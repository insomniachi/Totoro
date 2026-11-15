namespace Totoro.Playback;

/// <summary>
/// A Shazam signature of 16 kHz mono audio: the loudest points of its spectrogram, in four frequency bands, in the format
/// Shazam's app sends. Ported from shazam-for-real (Aleksey Martynov, ISC licence), itself after SongRec.
/// </summary>
internal sealed class ShazamSignature
{
	public const int SampleRate = 16000;

	// A spectrum every 8 ms, of the last 128 ms (as AudioFingerprint's frame: its FFT and window are reused).
	private const int ChunkSize = 128;
	private const int WindowSize = 2048;
	private const int BinCount = WindowSize / 2 + 1;

	// How far around a peak nothing may be louder: in time (stripes) and frequency (bins).
	private const int HStripes = 45, HBins = 1, VStripes = 3, VBins = 10;

	private static readonly int[] s_bandFreqs = [250, 520, 1450, 3500, 5500];
	private static readonly int s_minBin = Math.Max(FreqToBin(s_bandFreqs[0]), VBins);
	private static readonly int s_maxBin = Math.Min(FreqToBin(s_bandFreqs[^1]), BinCount - VBins);
	private const float MinMagnitude = 1f / 512 / 512;
	private static readonly float s_logMinMagnitude = MathF.Log(MinMagnitude);

	private readonly float[] _ring = new float[WindowSize];
	private readonly List<float[]> _stripes = [];
	private readonly List<(int Stripe, float Bin, float Magnitude)>[] _bands = [[], [], [], []];
	private readonly double[] _re = new double[WindowSize];
	private readonly double[] _im = new double[WindowSize];

	public int SampleCount { get; private set; }

	public int Milliseconds => SampleCount * 1000 / SampleRate;

	public void Add(ReadOnlySpan<float> samples)
	{
		foreach (var sample in samples)
		{
			_ring[SampleCount % WindowSize] = sample;
			SampleCount++;
			if (SampleCount % ChunkSize == 0 && SampleCount >= WindowSize)
			{
				AddStripe();
			}
		}
	}

	private void AddStripe()
	{
		// The ring's oldest sample is where the next one goes.
		var start = SampleCount % WindowSize;
		for (var i = 0; i < WindowSize; i++)
		{
			_re[i] = _ring[(start + i) % WindowSize] * AudioFingerprint.s_hann[i];
			_im[i] = 0;
		}

		AudioFingerprint.Fft(_re, _im);
		var stripe = new float[BinCount];
		for (var bin = 0; bin < BinCount; bin++)
		{
			// Scaled by 2 as Shazam's app has since 7.11.
			stripe[bin] = (float)(2 * (_re[bin] * _re[bin] + _im[bin] * _im[bin]));
		}

		_stripes.Add(stripe);
		if (_stripes.Count > 2 * HStripes)
		{
			FindPeaks(_stripes.Count - HStripes - 1);
		}
	}

	private void FindPeaks(int stripe)
	{
		for (var bin = s_minBin; bin < s_maxBin; bin++)
		{
			if (_stripes[stripe][bin] < MinMagnitude || !IsPeak(stripe, bin, HStripes, HBins) || !IsPeak(stripe, bin, VStripes, VBins))
			{
				continue;
			}

			// Quadratic interpolation of the peak's frequency, on log magnitudes.
			var (a, b, c) = (LogMagnitude(stripe, bin - 1), LogMagnitude(stripe, bin), LogMagnitude(stripe, bin + 1));
			var interpolated = bin + (a - c) / (a - 2 * b + c) / 2;
			var freq = interpolated * SampleRate / WindowSize;
			var band = Array.FindIndex(s_bandFreqs, 1, x => freq < x) - 1;
			if (freq >= s_bandFreqs[0] && band >= 0)
			{
				_bands[band].Add((stripe, interpolated, b));
			}
		}
	}

	private bool IsPeak(int stripe, int bin, int stripes, int bins)
	{
		var center = _stripes[stripe][bin];
		for (var s = -stripes; s <= stripes; s++)
		{
			for (var b = -bins; b <= bins; b++)
			{
				if ((s != 0 || b != 0) && _stripes[stripe + s][bin + b] >= center)
				{
					return false;
				}
			}
		}

		return true;
	}

	private float LogMagnitude(int stripe, int bin) => 18 * 1024 * (1 - MathF.Log(_stripes[stripe][bin]) / s_logMinMagnitude);

	private static int FreqToBin(float freq) => Convert.ToInt32(freq * WindowSize / SampleRate);

	/// <summary>The signature so far, as bytes to send.</summary>
	public byte[] Write()
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter(memory);
		writer.Write(0xCAFE2580); // magic
		writer.Write(0); // CRC, below
		writer.Write(0); // length, below
		writer.Write(VersionCode);
		writer.Write(0);
		writer.Write(0);
		writer.Write(0);
		writer.Write(3 << 27); // 16 kHz
		writer.Write(0);
		writer.Write(0);
		writer.Write(SampleCount);
		writer.Write(0x007C0000);
		writer.Write(0x40000000);
		writer.Write(0); // length, below

		var allowed = 12 + _stripes.Count / 12;
		for (var i = 0; i < _bands.Length; i++)
		{
			// The loudest peaks only, as Shazam's own signatures have.
			var peaks = _bands[i];
			if (peaks.Count > allowed)
			{
				peaks.Sort((x, y) => y.Magnitude.CompareTo(x.Magnitude));
				peaks.RemoveRange(allowed, peaks.Count - allowed);
				peaks.Sort((x, y) => x.Stripe.CompareTo(y.Stripe));
			}

			var data = BandData(peaks);
			writer.Write(0x60030040 + i);
			writer.Write(data.Length);
			writer.Write(data);
		}

		var length = (int)memory.Length;
		foreach (var offset in (int[])[8, 52])
		{
			memory.Position = offset;
			writer.Write(length - 48);
		}

		memory.Position = 4;
		writer.Write(Crc32(memory.GetBuffer().AsSpan(8, length - 8)));
		return memory.ToArray();
	}

	private static byte[] BandData(List<(int Stripe, float Bin, float Magnitude)> peaks)
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter(memory);
		var last = 0;
		foreach (var (stripe, bin, magnitude) in peaks)
		{
			if (stripe - last >= 100)
			{
				last = stripe;
				writer.Write((byte)255);
				writer.Write(last);
			}

			writer.Write(Convert.ToByte(stripe - last));
			writer.Write(Convert.ToUInt16(magnitude));
			writer.Write(Convert.ToUInt16(64 * bin));
			last = stripe;
		}

		while (memory.Length % 4 != 0)
		{
			writer.Write((byte)0);
		}

		return memory.ToArray();
	}

	// Extractor 10.1.3, unpacker 7.0.0.
	private const uint VersionCode = ((10u << 25) + (1u << 20) + (3u << 15) + (7u << 10)) ^ 0x80000000;

	private static readonly uint[] s_crcTable = [.. Enumerable.Range(0, 256).Select(n =>
	{
		var c = (uint)n;
		for (var k = 0; k < 8; k++)
		{
			c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
		}

		return c;
	})];

	/// <summary>The CRC-32 (IEEE) of <paramref name="bytes"/>.</summary>
	internal static uint Crc32(ReadOnlySpan<byte> bytes)
	{
		var crc = 0xFFFFFFFF;
		foreach (var b in bytes)
		{
			crc = s_crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
		}

		return ~crc;
	}
}
