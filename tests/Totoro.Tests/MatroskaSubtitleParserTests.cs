using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using Totoro.Modules.MediaPlayers.MediaFoundation;
using Xunit;

namespace Totoro.Tests;

public class MatroskaSubtitleParserTests
{
	[Fact]
	public void Reads_TheTracks_AndTheSubtitles_GivenInPieces()
	{
		var (file, _) = File();
		var header = new MatroskaHeader();
		var parser = new MatroskaSubtitleParser(header, 0);
		List<MatroskaCue> cues = [];
		for (var at = 0; at < file.Length; at += 7)
		{
			parser.Feed(file.AsSpan(at, Math.Min(7, file.Length - at)), cues);
		}

		Assert.True(header.Tracks.Task.IsCompleted);
		Assert.Equal([new MatroskaSubtitleTrack(2, "eng", "English", false, false), new MatroskaSubtitleTrack(3, "jpn", "", true, true)], header.Tracks.Task.Result);
		Assert.Equal(
		[
			.. s_cues,
		], cues);
	}

	private static readonly MatroskaCue[] s_cues =
	[
		new(2, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(2), "Hello there"),
		new(2, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(2), "A sign"),
		new(3, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(1), "First line\nSecond"),
		new(2, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(3), "Later"),
	];

	[Fact]
	public void Reads_FromElsewhere_FromTheNextCluster()
	{
		var (file, secondCluster) = File();
		var header = new MatroskaHeader();
		new MatroskaSubtitleParser(header, 0).Feed(file.AsSpan(0, 1000), []);
		Assert.True(header.Tracks.Task.IsCompleted);

		// From inside the first cluster's video.
		var parser = new MatroskaSubtitleParser(header, secondCluster - 3000);
		List<MatroskaCue> cues = [];
		parser.Feed(file.AsSpan((int)(secondCluster - 3000)), cues);

		Assert.Equal([new MatroskaCue(2, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(3), "Later")], cues);
	}

	[Fact]
	public void ReadAheadStream_PassesOn_TheSubtitlesItDownloads()
	{
		var (file, _) = File();
		using var stream = new ReadAheadStream(file.Length, (offset, _) =>
			Task.FromResult<Stream>(new MemoryStream(file, (int)offset, file.Length - (int)offset)), readSubtitles: true);

		Assert.True(stream.SubtitleTracks.Wait(TimeSpan.FromSeconds(5)));
		var cues = new BlockingCollection<MatroskaCue>();
		stream.OnSubtitle(cues.Add);
		for (var i = 0; i < s_cues.Length; i++)
		{
			Assert.True(cues.TryTake(out _, TimeSpan.FromSeconds(5)), $"Only {i} subtitles");
		}
	}

	[Fact]
	public void ReadAheadStream_ReadingOnPastTheStartsWindow_MissesNoSubtitles()
	{
		var (file, _) = File();
		// Windows of 1000 bytes: the start's stops inside the first cluster's video, before its subtitles.
		using var stream = new ReadAheadStream(file.Length, (offset, _) =>
			Task.FromResult<Stream>(new MemoryStream(file, (int)offset, file.Length - (int)offset)), capacity: 4000, readSubtitles: true);
		List<MatroskaCue> cues = [];
		stream.OnSubtitle(cue =>
		{
			lock (cues)
			{
				cues.Add(cue);
			}
		});

		stream.CopyTo(Stream.Null);
		Thread.Sleep(200);

		lock (cues)
		{
			Assert.Equal(s_cues.ToHashSet(), cues.ToHashSet());
		}
	}

	/// <summary>
	/// A Matroska file: a video track (1) and subtitle tracks, English (2, SRT) and Japanese (3, ASS, zlib-compressed); a
	/// cluster at 1 s with video, then subtitles at 1.5 s; a cluster at 60 s (starting with a CRC-32) with video, then a
	/// subtitle. Also where the second cluster starts.
	/// </summary>
	private static (byte[] File, long SecondCluster) File()
	{
		var tracks = Element(0x1654AE6B,
			Element(0xAE, Element(0xD7, 1), Element(0x83, 1), Element(0x86, "V_MPEG4/ISO/AVC")),
			Element(0xAE, Element(0xD7, 2), Element(0x83, 0x11), Element(0x86, "S_TEXT/UTF8"), Element(0x22B59C, "eng"), Element(0x536E, "English"), Element(0x88, 0)),
			Element(0xAE, Element(0xD7, 3), Element(0x83, 0x11), Element(0x86, "S_TEXT/ASS"), Element(0x22B59C, "jpn"), Element(0x55AA, 1),
				Element(0x6D80, Element(0x6240, Element(0x5034, Element(0x4254, 0))))));
		var start = Concat(
			Element(0x1A45DFA3, Element(0x4282, "matroska")),
			[0x18, 0x53, 0x80, 0x67, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], // the segment, of unknown size
			Element(0x1549A966, Element(0x2AD7B1, 1_000_000)),
			tracks);
		var first = Element(0x1F43B675,
			Element(0xE7, 1000),
			Element(0xA3, Block(1, 0, new byte[5000])),
			Element(0xA0, Element(0xA1, Block(2, 500, "Hello <i>there</i>"u8.ToArray())), Element(0x9B, 2000)),
			Element(0xA0, Element(0xA1, Block(2, 500, "A sign"u8.ToArray())), Element(0x9B, 2000)),
			Element(0xA0, Element(0xA1, Block(3, 500, Deflate("1,0,Default,,0,0,0,,{\\an8}First line\\NSecond"u8.ToArray()))), Element(0x9B, 1000)));
		var second = Element(0x1F43B675,
			// A CRC-32 first, as ffmpeg writes.
			[0xBF, 0x84, 1, 2, 3, 4],
			Element(0xE7, 60_000),
			Element(0xA3, Block(1, 0, new byte[8000])),
			Element(0xA3, Block(2, 0, "Later"u8.ToArray())));
		return (Concat(start, first, second), start.Length + first.Length);
	}

	private static byte[] Block(int track, short time, byte[] payload) => Concat([(byte)(0x80 | track), (byte)(time >> 8), (byte)time, 0], payload);

	private static byte[] Element(uint id, params byte[][] children) => Element(id, Concat(children));

	private static byte[] Element(uint id, string value) => Element(id, Encoding.UTF8.GetBytes(value));

	private static byte[] Element(uint id, long value) => Element(id, BitConverter.GetBytes(value).Reverse().SkipWhile(x => x == 0).DefaultIfEmpty((byte)0).ToArray());

	private static byte[] Element(uint id, byte[] payload)
	{
		var idBytes = BitConverter.GetBytes(id).Reverse().SkipWhile(x => x == 0).ToArray();
		// Its size in 4 bytes.
		byte[] size = [(byte)(0x10 | payload.Length >> 24), (byte)(payload.Length >> 16), (byte)(payload.Length >> 8), (byte)payload.Length];
		return Concat(idBytes, size, payload);
	}

	private static byte[] Deflate(byte[] data)
	{
		using var compressed = new MemoryStream();
		using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
		{
			zlib.Write(data);
		}

		return compressed.ToArray();
	}

	private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(x => x)];
}
