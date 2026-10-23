using System.Text.Json;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Playback;
using Xunit;

namespace Totoro.Tests;

public class AudioFingerprintTests
{
	private const int Rate = AudioFingerprint.SampleRate;

	// A "song": a new random note (two tones) every quarter second.
	private static float[] Song(double seconds, int seed)
	{
		var random = new Random(seed);
		var samples = new float[(int)(seconds * Rate)];
		double a = 0, b = 0;
		for (var i = 0; i < samples.Length; i++)
		{
			if (i % (Rate / 4) == 0)
			{
				(a, b) = (300 + random.NextDouble() * 1500, 300 + random.NextDouble() * 1500);
			}

			samples[i] = (float)(0.3 * Math.Sin(2 * Math.PI * a * i / Rate) + 0.2 * Math.Sin(2 * Math.PI * b * i / Rate));
		}

		return samples;
	}

	private static float[] Noise(double seconds, int seed)
	{
		var random = new Random(seed);
		return [.. Enumerable.Range(0, (int)(seconds * Rate)).Select(_ => (float)(random.NextDouble() - 0.5) * 0.2f)];
	}

	[Fact]
	public void Find_SongInEpisode()
	{
		var song = Song(20, 1);
		var episode = Noise(120, 2);
		var at = (int)(37.5 * Rate);
		for (var i = 0; i < song.Length; i++)
		{
			// Quieter, with the episode's noise over it, like dialogue over an opening.
			episode[at + i] += song[i] * 0.7f;
		}

		var found = AudioFingerprint.Find(AudioFingerprint.Compute(song), AudioFingerprint.Compute(episode));

		Assert.NotNull(found);
		Assert.InRange(found.Value.Start.TotalSeconds, 37.4, 37.6);
	}

	[Fact]
	public void Find_SongNotInEpisode()
	{
		var episode = Noise(120, 2);
		var other = Song(20, 3);
		other.CopyTo(episode, Rate * 50);

		Assert.Null(AudioFingerprint.Find(AudioFingerprint.Compute(Song(20, 1)), AudioFingerprint.Compute(episode)));
	}

	[Theory]
	[InlineData("1–12", 5, true)]
	[InlineData("1–12", 13, false)]
	[InlineData("1, 3–5", 2, false)]
	[InlineData("1, 3–5", 4, true)]
	[InlineData("7", 7, true)]
	[InlineData("", 3, true)]
	[InlineData("BD", 3, true)]
	public void Covers(string episodes, int number, bool expected) => Assert.Equal(expected, AudioFingerprint.Covers(episodes, number));

	[Fact]
	public void DownloadInfo_KeepsSegments_AndReadsOldFiles()
	{
		var info = new DownloadInfo("Frieren", [], 1, 2, new Dictionary<int, IReadOnlyList<MediaSegment>>
		{
			[5] = [new MediaSegment(MediaSegmentType.Opening, TimeSpan.FromSeconds(90.5), TimeSpan.FromSeconds(180.5))],
		});

		var read = JsonSerializer.Deserialize<DownloadInfo>(JsonSerializer.Serialize(info))!;
		Assert.Equal(info.Segments![5], read.Segments![5]);
		Assert.Null(JsonSerializer.Deserialize<DownloadInfo>("""{"Title":"Frieren","OtherTitles":[]}""")!.Segments);
	}
}
