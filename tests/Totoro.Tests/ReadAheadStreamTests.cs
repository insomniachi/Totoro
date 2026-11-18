using System.Collections.Concurrent;
using Totoro.Modules.MediaPlayers.MediaFoundation;
using Xunit;

namespace Totoro.Tests;

public class ReadAheadStreamTests
{
	private static readonly byte[] s_file = [.. Enumerable.Range(0, 10_000).Select(i => (byte)(i * 7))];

	[Fact]
	public void Reads_TheFile_ThroughABufferSmallerThanIt_AndSeeks()
	{
		var opened = new ConcurrentBag<long>();
		using var stream = new ReadAheadStream(s_file.Length, (offset, _) =>
		{
			opened.Add(offset);
			return Task.FromResult<Stream>(new MemoryStream(s_file, (int)offset, s_file.Length - (int)offset));
		}, capacity: 1024);

		Assert.Equal(s_file, ReadToEnd(stream));

		// Back to before what's buffered (past the start, which is kept): downloaded again from there.
		stream.Position = 5000;
		Assert.Equal(s_file[5000..], ReadToEnd(stream));
		Assert.Contains(5000, opened);

		// The start is kept.
		stream.Position = 100;
		Assert.Equal(s_file[100..200], ReadToEnd(stream)[..100]);
		Assert.DoesNotContain(100, opened);

		stream.Seek(-50, SeekOrigin.End);
		Assert.Equal(s_file[^50..], ReadToEnd(stream));
	}

	[Fact]
	public void Reads_GoingBackAndForthBetweenTwoParts_DontDownloadAgainEachTime()
	{
		var opened = new ConcurrentBag<long>();
		using var stream = new ReadAheadStream(s_file.Length, (offset, _) =>
		{
			opened.Add(offset);
			return Task.FromResult<Stream>(new MemoryStream(s_file, (int)offset, s_file.Length - (int)offset));
		}, capacity: 3000);

		var buffer = new byte[100];
		for (var i = 0; i < 8; i++)
		{
			foreach (var at in new[] { 1000 + i * 100, 6000 + i * 100 })
			{
				stream.Position = at;
				stream.ReadExactly(buffer);
				Assert.Equal(s_file[at..(at + 100)], buffer);
			}
		}

		Assert.True(opened.Count <= 3, $"Downloaded from {opened.Count} places: {string.Join(", ", opened)}");
	}

	[Fact]
	public void Reads_GoingBackToTheStart_AsItReadsOn_DontDownloadAgainEachTime()
	{
		var opened = new ConcurrentBag<long>();
		using var stream = new ReadAheadStream(s_file.Length, (offset, _) =>
		{
			opened.Add(offset);
			return Task.FromResult<Stream>(new MemoryStream(s_file, (int)offset, s_file.Length - (int)offset));
		}, capacity: 3000);

		var buffer = new byte[100];
		for (var at = 0; at < s_file.Length; at += 100)
		{
			foreach (var position in new[] { at, 0 })
			{
				stream.Position = position;
				stream.ReadExactly(buffer);
				Assert.Equal(s_file[position..(position + 100)], buffer);
			}
		}

		Assert.True(opened.Count <= 3, $"Downloaded from {opened.Count} places: {string.Join(", ", opened)}");
	}

	[Fact]
	public void Read_JustPastAFullWindow_GoesOnDownloading()
	{
		using var stream = new ReadAheadStream(s_file.Length, (offset, _) =>
			Task.FromResult<Stream>(new MemoryStream(s_file, (int)offset, s_file.Length - (int)offset)), capacity: 3000);

		// A window (750 bytes) from 1000 fills, read only at its first byte.
		stream.Position = 1000;
		stream.ReadByte();
		Thread.Sleep(200);

		stream.Position = 1750;
		var read = Task.Run(stream.ReadByte);
		Assert.True(read.Wait(TimeSpan.FromSeconds(5)), "The read waited for good");
		Assert.Equal(s_file[1750], read.Result);
	}

	[Fact]
	public void Read_Throws_WhenTheDownloadFails()
	{
		using var stream = new ReadAheadStream(100, (_, _) => Task.FromException<Stream>(new HttpRequestException("gone")), capacity: 64);
		Assert.Throws<IOException>(() => stream.ReadByte());
	}

	private static byte[] ReadToEnd(Stream stream)
	{
		using var copy = new MemoryStream();
		var buffer = new byte[300];
		int read;
		while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
		{
			copy.Write(buffer, 0, read);
		}

		return copy.ToArray();
	}
}
