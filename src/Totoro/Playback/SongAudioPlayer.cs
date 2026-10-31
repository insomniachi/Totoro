using System.Numerics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using NAudio.Wave;
using Totoro.Core.Services;
using Totoro.Core.Settings;
using Totoro.Modules.MediaPlayers.Vlc;

namespace Totoro.Playback;

/// <summary>
/// Plays songs' audio in the app (see <see cref="ISongPlayer"/>): libvlc decodes it (Opus, from AnimeThemes) and hands
/// over the samples, which play through WASAPI (NAudio). The samples are tapped as WASAPI takes them, so the spectrum
/// shows what's heard, not what's decoded ahead of it. Uses the built-in player's volume.
/// </summary>
internal sealed class SongAudioPlayer : ISongPlayer, IDisposable
{
	private const int SampleRate = 48000;
	private const int Channels = 2;
	// Samples the spectrum is taken over: about 43 ms.
	private const int FftSize = 2048;

	private readonly IModuleSettings<Modules.MediaPlayers.Vlc.Settings> _vlcSettings;
	private readonly ILogger<SongAudioPlayer> _logger;
	private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();
	private readonly Lock _tapLock = new();
	private readonly float[] _tap = new float[FftSize];
	private int _tapPosition;

	// libvlc's callbacks; kept, so the delegates libvlc holds aren't collected.
	private readonly MediaPlayer.LibVLCAudioPlayCb _onPlay;
	private readonly MediaPlayer.LibVLCAudioPauseCb _onPause;
	private readonly MediaPlayer.LibVLCAudioResumeCb _onResume;
	private readonly MediaPlayer.LibVLCAudioFlushCb _onFlush;
	private readonly MediaPlayer.LibVLCAudioDrainCb _onDrain;

	private LibVLC? _libVlc;
	private MediaPlayer? _player;
	private BufferedWaveProvider? _buffer;
	private WasapiPlayer? _output;
	private byte[] _samples = [];

	// libvlc reports the time about once a second; read more often, for the progress bar.
	private DispatcherQueueTimer? _positionTimer;

	public SongAudioPlayer(IModuleSettings<Modules.MediaPlayers.Vlc.Settings> vlcSettings, ILogger<SongAudioPlayer> logger)
	{
		_vlcSettings = vlcSettings;
		_logger = logger;
		_onPlay = OnSamples;
		_onPause = (_, _) => _output?.Pause();
		_onResume = (_, _) => _output?.Play();
		_onFlush = (_, _) => _buffer?.ClearBuffer();
		_onDrain = _ => { };
	}

	public AnimeSong? Current { get; private set; }

	public bool IsPlaying { get; private set; }

	public bool IsLoading { get; private set; }

	public TimeSpan Position { get; private set; }

	public TimeSpan Duration { get; private set; }

	public async Task PlayAsync(AnimeSong song)
	{
		if (song.Audio is not { } audio)
		{
			return;
		}

		Current = song;
		IsPlaying = true;
		IsLoading = true;
		Position = Duration = TimeSpan.Zero;
		Changed();

		var player = await EnsureCreatedAsync();
		if (!ReferenceEquals(Current, song))
		{
			// Another song was played meanwhile.
			return;
		}

		_buffer!.ClearBuffer();
		ClearTap();
		_output!.Volume = Math.Clamp(_vlcSettings.Value.Volume / 100f, 0, 1);
		using var media = new Media(_libVlc!, audio);
		// Replacing what's playing stops it, which can take a moment: not on the UI thread.
		await Task.Run(() => player.Play(media));
		_output.Play();
		StartPositionTimer();
	}

	public void Pause()
	{
		if (Current is null || !IsPlaying)
		{
			return;
		}

		_player?.SetPause(true);
		_output?.Pause();
		IsPlaying = false;
		Changed();
	}

	public void Resume()
	{
		if (Current is null || IsPlaying)
		{
			return;
		}

		_player?.SetPause(false);
		_output?.Play();
		IsPlaying = true;
		Changed();
	}

	public void Stop()
	{
		if (Current is null)
		{
			return;
		}

		Current = null;
		IsPlaying = false;
		IsLoading = false;
		Position = Duration = TimeSpan.Zero;
		_positionTimer?.Stop();
		_output?.Stop();
		_buffer?.ClearBuffer();
		ClearTap();
		if (_player is { } player)
		{
			_ = Task.Run(player.Stop);
		}

		Changed();
	}

	public void GetLevels(Span<float> bands)
	{
		Span<Complex> spectrum = stackalloc Complex[FftSize];
		lock (_tapLock)
		{
			// Oldest first, with a Hann window so the ends don't smear the spectrum.
			for (var i = 0; i < FftSize; i++)
			{
				var window = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (FftSize - 1));
				spectrum[i] = new Complex(_tap[(_tapPosition + i) % FftSize] * window, 0);
			}
		}

		Fourier.Transform(spectrum);

		// Bands spaced evenly in pitch (logarithmically), from about 40 Hz to 16 kHz.
		const double Lowest = 40, Highest = 16000;
		var binWidth = (double)SampleRate / FftSize;
		for (var band = 0; band < bands.Length; band++)
		{
			var from = Lowest * Math.Pow(Highest / Lowest, (double)band / bands.Length);
			var to = Lowest * Math.Pow(Highest / Lowest, (double)(band + 1) / bands.Length);
			int first = (int)(from / binWidth), last = Math.Max(first + 1, (int)(to / binWidth));
			double peak = 0;
			for (var bin = first; bin < last && bin < FftSize / 2; bin++)
			{
				peak = Math.Max(peak, spectrum[bin].Magnitude);
			}

			// Decibels, from -60 (nothing) to 0 (full scale), as 0 to 1.
			var decibels = 20 * Math.Log10(peak / (FftSize / 4) + 1e-9);
			bands[band] = (float)Math.Clamp((decibels + 60) / 60, 0, 1);
		}
	}

	public void Dispose()
	{
		_output?.Dispose();
		_player?.Dispose();
		_libVlc?.Dispose();
	}

	private async Task<MediaPlayer> EnsureCreatedAsync()
	{
		if (_player is { } existing)
		{
			return existing;
		}

		var (libVlc, player) = await Task.Run(() =>
		{
			LibVlcLoader.Load();
			var libVlc = new LibVLC(enableDebugLogs: false, "--no-video");
			return (libVlc, new MediaPlayer(libVlc));
		});
		_libVlc = libVlc;
		// The callbacks first: the format only applies to them.
		player.SetAudioCallbacks(_onPlay, _onPause, _onResume, _onFlush, _onDrain);
		player.SetAudioFormat("S16N", SampleRate, Channels);
		player.LengthChanged += (_, e) => OnUi(() =>
		{
			Duration = TimeSpan.FromMilliseconds(e.Length);
			Changed();
		});
		player.EndReached += (_, _) => OnUi(Stop);
		player.EncounteredError += (_, _) => OnUi(() =>
		{
			_logger.LogWarning("Couldn't play {Song}", Current?.Audio);
			Stop();
		});

		_buffer = new BufferedWaveProvider(new WaveFormat(SampleRate, 16, Channels), TimeSpan.FromSeconds(5)) { DiscardOnBufferOverflow = true };
		_output = new WasapiPlayerBuilder().WithSharedMode().WithLatency(100).Build();
		_output.Init(new TappedProvider(_buffer, this));
		_player = player;
		return player;
	}

	/// <summary>libvlc's decoded samples (16-bit, interleaved stereo), queued for WASAPI.</summary>
	private void OnSamples(IntPtr data, IntPtr samples, uint count, long pts)
	{
		var bytes = (int)count * Channels * sizeof(short);
		if (_samples.Length < bytes)
		{
			_samples = new byte[bytes];
		}

		Marshal.Copy(samples, _samples, 0, bytes);
		_buffer?.AddSamples(_samples, 0, bytes);
	}

	/// <summary>Keeps the last samples WASAPI took (as mono), for the spectrum.</summary>
	private void Tap(ReadOnlySpan<byte> played)
	{
		var frames = MemoryMarshal.Cast<byte, short>(played);
		lock (_tapLock)
		{
			for (var i = 0; i + 1 < frames.Length; i += Channels)
			{
				_tap[_tapPosition] = (frames[i] + frames[i + 1]) / (2f * short.MaxValue);
				_tapPosition = (_tapPosition + 1) % FftSize;
			}
		}
	}

	private void ClearTap()
	{
		lock (_tapLock)
		{
			Array.Clear(_tap);
		}
	}

	private void StartPositionTimer()
	{
		if (_positionTimer is null)
		{
			_positionTimer = _ui.CreateTimer();
			_positionTimer.Interval = TimeSpan.FromMilliseconds(250);
			_positionTimer.Tick += (_, _) =>
			{
				if (_player is { } player && Current is not null && IsPlaying)
				{
					Position = TimeSpan.FromMilliseconds(Math.Max(0, player.Time));
					// The time moves once the audio's heard.
					IsLoading &= Position == TimeSpan.Zero;
					Changed();
				}
			};
		}

		_positionTimer.Start();
	}

	private void Changed() => WeakReferenceMessenger.Default.Send(new SongPlayerChangedMessage(this));

	// libvlc raises its events on its own threads, which mustn't call back into it either.
	private void OnUi(Action action) => _ui.TryEnqueue(() => action());

	/// <summary>The buffered samples, tapped for the spectrum as WASAPI reads them.</summary>
	private sealed class TappedProvider(IWaveProvider source, SongAudioPlayer player) : IWaveProvider
	{
		public WaveFormat WaveFormat => source.WaveFormat;

		public int Read(Span<byte> buffer)
		{
			var read = source.Read(buffer);
			player.Tap(buffer[..read]);
			return read;
		}
	}

	/// <summary>An in-place radix-2 fast Fourier transform.</summary>
	private static class Fourier
	{
		public static void Transform(Span<Complex> values)
		{
			var n = values.Length;
			for (int i = 1, j = 0; i < n; i++)
			{
				var bit = n >> 1;
				for (; (j & bit) != 0; bit >>= 1)
				{
					j ^= bit;
				}

				j ^= bit;
				if (i < j)
				{
					(values[i], values[j]) = (values[j], values[i]);
				}
			}

			for (var length = 2; length <= n; length <<= 1)
			{
				var angle = -2 * Math.PI / length;
				var step = new Complex(Math.Cos(angle), Math.Sin(angle));
				for (var start = 0; start < n; start += length)
				{
					var w = Complex.One;
					for (var k = 0; k < length / 2; k++)
					{
						var even = values[start + k];
						var odd = values[start + k + length / 2] * w;
						values[start + k] = even + odd;
						values[start + k + length / 2] = even - odd;
						w *= step;
					}
				}
			}
		}
	}
}
