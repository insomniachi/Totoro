using System.Numerics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using Concentus;
using Concentus.Oggfile;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using NAudio.Wave;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Totoro.Core.Settings;

namespace Totoro.Playback;

/// <summary>
/// Plays songs' audio in the app (see <see cref="ISongPlayer"/>): the audio (Opus in Ogg, from AnimeThemes, which
/// Windows' own decoders don't read) is downloaded, decoded with Concentus and played through WASAPI (NAudio). The
/// samples are tapped as WASAPI takes them, so the spectrum shows what's heard, not what's decoded ahead of it. Uses
/// mpv's player volume (mpv being the default player).
/// </summary>
internal sealed class SongAudioPlayer : ISongPlayer, IDisposable
{
	private const int SampleRate = 48000;
	private const int Channels = 2;
	// Samples the spectrum is taken over: about 43 ms.
	private const int FftSize = 2048;
	// How far ahead of what's heard it decodes.
	private static readonly TimeSpan s_decodeAhead = TimeSpan.FromSeconds(2);
	// AnimeThemes refuses requests without one.
	private const string UserAgent = "Totoro (anime tracker)";

	private readonly IModuleSettings<Modules.MediaPlayers.Mpv.Settings> _playerSettings;
	private readonly ILogger<SongAudioPlayer> _logger;
	private readonly DispatcherQueue _ui = DispatcherQueue.GetForCurrentThread();
	private readonly Lock _tapLock = new();
	private readonly float[] _tap = new float[FftSize];
	private int _tapPosition;

	private BufferedWaveProvider? _buffer;
	private WasapiPlayer? _output;
	// The song playing's download and decoding, cancelled when it's stopped or replaced.
	private CancellationTokenSource? _song;

	// Read more often than it changes much, for the progress bar.
	private DispatcherQueueTimer? _positionTimer;

	// Frames (a sample per channel) WASAPI has taken of the song: where it is.
	private long _playedFrames;

	// Where to seek the song to, in frames, for the decoder to take up; -1 when not seeking.
	private long _seekTo = -1;

	public SongAudioPlayer(IModuleSettings<Modules.MediaPlayers.Mpv.Settings> playerSettings, ILogger<SongAudioPlayer> logger)
	{
		_playerSettings = playerSettings;
		_logger = logger;
	}

	public AnimeSong? Current { get; private set; }

	public bool IsPlaying { get; private set; }

	public bool IsLoading { get; private set; }

	public TimeSpan Position
	{
		get
		{
			var position = TimeSpan.FromSeconds(Interlocked.Read(ref _playedFrames) / (double)SampleRate);
			return Duration > TimeSpan.Zero && position > Duration ? Duration : position;
		}
	}

	public TimeSpan Duration { get; private set; }

	public event EventHandler? Ended;

	public event EventHandler? Failed;

	public AnimeModel? Anime { get; private set; }

	public async Task PlayAsync(AnimeSong song, AnimeModel? anime = null)
	{
		if (song.Audio is not { } audio)
		{
			return;
		}

		_song?.Cancel();
		var cts = _song = new CancellationTokenSource();
		Current = song;
		Anime = anime;
		IsPlaying = true;
		IsLoading = true;
		Duration = TimeSpan.Zero;
		CreateOutput();
		_buffer!.ClearBuffer();
		ClearTap();
		Interlocked.Exchange(ref _playedFrames, 0);
		Interlocked.Exchange(ref _seekTo, -1);
		Changed();

		byte[] bytes;
		OpusOggReadStream reader;
		try
		{
			// Concentus reads Ogg from a seekable stream only: the whole file (a few MB), then.
			bytes = await audio.WithHeader("User-Agent", UserAgent).GetBytesAsync(cancellationToken: cts.Token);
			reader = Read(bytes);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cts.IsCancellationRequested)
		{
			_logger.LogWarning(ex, "Couldn't play {Song}", audio);
			FinishWith(Failed, cts);

			return;
		}
		catch (OperationCanceledException)
		{
			return;
		}

		if (cts.IsCancellationRequested)
		{
			return;
		}

		Duration = reader.TotalTime;
		_output.Volume = Math.Clamp(_playerSettings.Value.Volume / 100f, 0, 1);
		_ = Task.Run(() => Decode(bytes, reader, cts));
		if (IsPlaying)
		{
			_output.Play();
		}

		StartPositionTimer();
		Changed();
	}

	public void Pause()
	{
		if (Current is null || !IsPlaying)
		{
			return;
		}

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

		_song?.Cancel();
		_song = null;
		Current = null;
		Anime = null;
		IsPlaying = false;
		IsLoading = false;
		Duration = TimeSpan.Zero;
		Interlocked.Exchange(ref _playedFrames, 0);
		_positionTimer?.Stop();
		_output?.Stop();
		_buffer?.ClearBuffer();
		ClearTap();
		Changed();
	}

	public void Seek(TimeSpan position)
	{
		if (Current is null || Duration <= TimeSpan.Zero)
		{
			return;
		}

		var frames = (long)(Math.Clamp(position / Duration, 0, 1) * Duration.TotalSeconds * SampleRate);
		Interlocked.Exchange(ref _seekTo, frames);
		// Shown at once; the decoder sets it again once the old audio's gone.
		Interlocked.Exchange(ref _playedFrames, frames);
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
		_song?.Cancel();
		_output?.Dispose();
	}

	/// <summary>
	/// A new output for each song, on the device that's the default now: an output stays on the device it was made on, and
	/// goes silent for good once that device goes (e.g. headphones disconnecting, or another device made the default).
	/// </summary>
	// shortcut: a device lost mid-song stays silent until the next song; follow device changes if that's a problem.
	private void CreateOutput()
	{
		_output?.Dispose();
		_buffer ??= new BufferedWaveProvider(new WaveFormat(SampleRate, 16, Channels), s_decodeAhead * 2) { DiscardOnBufferOverflow = true };
		_output = new WasapiPlayerBuilder().WithSharedMode().WithLatency(100).Build();
		_output.Init(new TappedProvider(_buffer, this));
	}

	private static OpusOggReadStream Read(byte[] song) => new(OpusCodecFactory.CreateDecoder(SampleRate, Channels), new MemoryStream(song));

	/// <summary>
	/// Decodes the song into the buffer, keeping a little ahead of what's heard, then stops once it's all heard; seeks when
	/// asked (see <see cref="Seek"/>), even after it's all decoded.
	/// </summary>
	private async Task Decode(byte[] song, OpusOggReadStream reader, CancellationTokenSource cts)
	{
		var ct = cts.Token;
		var buffer = _buffer!;
		try
		{
			while (!ct.IsCancellationRequested)
			{
				if (Interlocked.Exchange(ref _seekTo, -1) is var seekTo and >= 0)
				{
					// A new reader, since one that's read to its end may not read again.
					reader = Read(song);
					reader.SeekTo(TimeSpan.FromSeconds(seekTo / (double)SampleRate));
					buffer.ClearBuffer();
					Interlocked.Exchange(ref _playedFrames, seekTo);
				}

				if (reader.HasNextPacket && buffer.BufferedDuration <= s_decodeAhead)
				{
					if (reader.DecodeNextPacket() is { Length: > 0 } samples)
					{
						var bytes = MemoryMarshal.AsBytes(samples.AsSpan());
						buffer.AddSamples(bytes.ToArray(), 0, bytes.Length);
					}
				}
				else if (reader.HasNextPacket || buffer.BufferedBytes > 0)
				{
					await Task.Delay(50, ct);
				}
				else
				{
					break;
				}
			}

			ct.ThrowIfCancellationRequested();
			OnUi(() => FinishWith(Ended, cts));
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Couldn't decode {Song}", Current?.Audio);
			OnUi(() => FinishWith(Failed, cts));
		}
	}

	/// <summary>
	/// The song of <paramref name="cts"/> ended or failed: raises <paramref name="finished"/>, then stops, unless a handler
	/// played another song (or it was replaced or stopped already).
	/// </summary>
	private void FinishWith(EventHandler? finished, CancellationTokenSource cts)
	{
		if (!ReferenceEquals(_song, cts))
		{
			return;
		}

		finished?.Invoke(this, EventArgs.Empty);
		if (ReferenceEquals(_song, cts))
		{
			Stop();
		}
	}

	/// <summary>
	/// Keeps the last samples WASAPI took (as mono), for the spectrum, and counts the song's own among them (the first
	/// <paramref name="decoded"/> bytes: the rest is silence, while it's still being decoded) for the position.
	/// </summary>
	private void Tap(ReadOnlySpan<byte> played, int decoded)
	{
		var frames = MemoryMarshal.Cast<byte, short>(played);
		if (Current is not null && IsPlaying)
		{
			Interlocked.Add(ref _playedFrames, Math.Min(played.Length, decoded) / sizeof(short) / Channels);
		}

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
				if (Current is not null && IsPlaying)
				{
					// The position moves once the audio's heard.
					IsLoading &= Position == TimeSpan.Zero;
					Changed();
				}
			};
		}

		_positionTimer.Start();
	}

	private void Changed() => WeakReferenceMessenger.Default.Send(new SongPlayerChangedMessage(this));

	private void OnUi(Action action) => _ui.TryEnqueue(() => action());

	/// <summary>The buffered samples, tapped for the spectrum as WASAPI reads them.</summary>
	private sealed class TappedProvider(BufferedWaveProvider source, SongAudioPlayer player) : IWaveProvider
	{
		public WaveFormat WaveFormat => source.WaveFormat;

		public int Read(Span<byte> buffer)
		{
			// What's decoded, before the read pads it out with silence.
			var decoded = source.BufferedBytes;
			var read = source.Read(buffer);
			player.Tap(buffer[..read], decoded);
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
