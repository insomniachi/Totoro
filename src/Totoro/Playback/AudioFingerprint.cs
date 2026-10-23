using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Totoro.Playback;

/// <summary>
/// Finds a song in an episode by its sound (Haitsma and Kalker's fingerprint): each 46 ms of audio becomes 32 bits, saying
/// how the loudness of 33 bands from 300 to 2000 Hz changes from band to band and from the 46 ms before. The bits hardly
/// change with the encoding, the volume, or voices over the song, so the song is where its bits best match the episode's.
/// </summary>
internal static class AudioFingerprint
{
	// 0.37 s frames, a new one every 46 ms (Haitsma and Kalker's, at a quarter of their overlap).
	public const int SampleRate = 5512;
	private const int FrameSize = 2048;
	private const int Hop = 256;
	private const int Bands = 33;

	// Where the song is, at most this share of the bits differ; anywhere else, about half do.
	private const double MaxErrorRate = 0.35;

	private static readonly double[] s_hann = [.. Enumerable.Range(0, FrameSize).Select(i => 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (FrameSize - 1)))];

	// The bands' edges as FFT bins, spaced evenly in pitch.
	private static readonly int[] s_edges = [.. Enumerable.Range(0, Bands + 1).Select(i => (int)(300 * Math.Pow(2000.0 / 300, (double)i / Bands) * FrameSize / SampleRate))];

	private static readonly double[] s_cos = [.. Enumerable.Range(0, FrameSize / 2).Select(i => Math.Cos(2 * Math.PI * i / FrameSize))];
	private static readonly double[] s_sin = [.. Enumerable.Range(0, FrameSize / 2).Select(i => -Math.Sin(2 * Math.PI * i / FrameSize))];

	/// <summary>How much audio each fingerprint word moves on by.</summary>
	public static TimeSpan FrameDuration { get; } = TimeSpan.FromSeconds((double)Hop / SampleRate);

	/// <summary>How long <paramref name="samples"/> (as <see cref="DecodeAsync"/> gives them) last.</summary>
	public static TimeSpan Duration(float[] samples) => TimeSpan.FromSeconds((double)samples.Length / SampleRate);

	/// <summary>
	/// The audio of a file as mono samples at the rate fingerprints are taken at, decoded by <paramref name="ffmpeg"/> at
	/// below-normal priority, so it doesn't slow anything the user is doing. A file, not a URL: mpv's ffmpeg can't check
	/// HTTPS certificates (it has no CA store).
	/// </summary>
	public static async Task<float[]> DecodeAsync(string ffmpeg, string input, CancellationToken ct)
	{
		var start = new ProcessStartInfo(ffmpeg)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};
		foreach (var arg in (string[])["-nostdin", "-v", "error", "-i", input, "-vn", "-ac", "1", "-ar", $"{SampleRate}", "-f", "s16le", "-"])
		{
			start.ArgumentList.Add(arg);
		}

		using var process = Process.Start(start) ?? throw new InvalidOperationException("ffmpeg didn't start.");
		try
		{
			process.PriorityClass = ProcessPriorityClass.BelowNormal;
		}
		catch (InvalidOperationException)
		{
			// Already done.
		}

		using var kill = ct.Register(() =>
		{
			try
			{
				process.Kill();
			}
			catch (InvalidOperationException)
			{
			}
		});
		var error = process.StandardError.ReadToEndAsync(ct);
		using var pcm = new MemoryStream();
		await process.StandardOutput.BaseStream.CopyToAsync(pcm, ct);
		await process.WaitForExitAsync(ct);
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"ffmpeg couldn't read {input}: {(await error).Trim()}");
		}

		var shorts = MemoryMarshal.Cast<byte, short>(pcm.GetBuffer().AsSpan(0, (int)pcm.Length));
		var samples = new float[shorts.Length];
		for (var i = 0; i < shorts.Length; i++)
		{
			samples[i] = shorts[i] / 32768f;
		}

		return samples;
	}

	/// <summary>The fingerprint of <paramref name="samples"/>: a word for each <see cref="FrameDuration"/>, from the second frame.</summary>
	public static uint[] Compute(ReadOnlySpan<float> samples)
	{
		var frames = (samples.Length - FrameSize) / Hop + 1;
		if (frames < 2)
		{
			return [];
		}

		var re = new double[FrameSize];
		var im = new double[FrameSize];
		var energy = new double[Bands];
		var previous = new double[Bands];
		var words = new uint[frames - 1];
		for (var frame = 0; frame < frames; frame++)
		{
			var window = samples.Slice(frame * Hop, FrameSize);
			for (var i = 0; i < FrameSize; i++)
			{
				re[i] = window[i] * s_hann[i];
				im[i] = 0;
			}

			Fft(re, im);
			for (var band = 0; band < Bands; band++)
			{
				double sum = 0;
				for (var bin = s_edges[band]; bin < s_edges[band + 1]; bin++)
				{
					sum += re[bin] * re[bin] + im[bin] * im[bin];
				}

				energy[band] = sum;
			}

			if (frame > 0)
			{
				uint word = 0;
				for (var bit = 0; bit < Bands - 1; bit++)
				{
					if (energy[bit] - energy[bit + 1] - (previous[bit] - previous[bit + 1]) > 0)
					{
						word |= 1u << bit;
					}
				}

				words[frame - 1] = word;
			}

			(energy, previous) = (previous, energy);
		}

		return words;
	}

	/// <summary>Where <paramref name="song"/> starts in <paramref name="episode"/> (both from <see cref="Compute"/>), and the share of bits that differ there; null if it isn't in it.</summary>
	public static (TimeSpan Start, double ErrorRate)? Find(uint[] song, uint[] episode)
	{
		if (song.Length == 0 || song.Length > episode.Length)
		{
			return null;
		}

		// shortcut: the whole song has to be there; an opening cut short (e.g. in a final episode) isn't found. Match the
		// longest run of close words instead if that's common.
		var (best, bestErrors) = (0, long.MaxValue);
		for (var offset = 0; offset + song.Length <= episode.Length; offset++)
		{
			long errors = 0;
			for (var i = 0; i < song.Length && errors < bestErrors; i++)
			{
				errors += BitOperations.PopCount(song[i] ^ episode[offset + i]);
			}

			if (errors < bestErrors)
			{
				(best, bestErrors) = (offset, errors);
			}
		}

		var rate = bestErrors / (32.0 * song.Length);
		return rate <= MaxErrorRate ? (best * FrameDuration, rate) : null;
	}

	/// <summary>Whether AnimeThemes' list of a song's episodes ("1–12, 14") has <paramref name="number"/>; true when the list is empty or unreadable.</summary>
	public static bool Covers(string episodes, int number)
	{
		var parts = episodes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		var covered = parts.Length == 0;
		foreach (var part in parts)
		{
			var ends = part.Split(['–', '-'], StringSplitOptions.TrimEntries);
			if (ends.Length > 2 || !int.TryParse(ends[0], out var from) || !int.TryParse(ends[^1], out var to))
			{
				return true;
			}

			covered |= number >= from && number <= to;
		}

		return covered;
	}

	/// <summary>In-place radix-2 FFT of a <see cref="FrameSize"/>-long frame.</summary>
	private static void Fft(double[] re, double[] im)
	{
		var n = re.Length;
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
				(re[i], re[j]) = (re[j], re[i]);
				(im[i], im[j]) = (im[j], im[i]);
			}
		}

		for (var length = 2; length <= n; length <<= 1)
		{
			var half = length / 2;
			var step = n / length;
			for (var i = 0; i < n; i += length)
			{
				for (var k = 0; k < half; k++)
				{
					var (wr, wi) = (s_cos[k * step], s_sin[k * step]);
					var (a, b) = (i + k, i + k + half);
					var tr = re[b] * wr - im[b] * wi;
					var ti = re[b] * wi + im[b] * wr;
					(re[b], im[b]) = (re[a] - tr, im[a] - ti);
					(re[a], im[a]) = (re[a] + tr, im[a] + ti);
				}
			}
		}
	}
}
