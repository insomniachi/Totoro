using System.Net;
using System.Net.Http.Headers;

namespace Totoro.Modules.MediaPlayers.MediaFoundation;

/// <summary>
/// A video file read over HTTP for Windows' player, downloaded ahead of where it's read into memory, never to disk:
/// Windows' own reader keeps only a few seconds ahead, so any slow response from the host stalls the video.
/// </summary>
/// <remarks>
/// Windows' reader goes back and forth between parts of a file (a Matroska file's tracks can be far apart), so what's
/// downloaded is kept in a few windows, each downloading on from where it was last read (a little kept behind). A read
/// away from all of them (a seek) starts the least recently used one again from there. The first window keeps the
/// file's start, which the reader keeps going back to as it plays from there.
/// </remarks>
internal sealed class ReadAheadStream : Stream
{
	public const int DefaultCapacity = 128 * 1024 * 1024;
	private const int WindowCount = 4;
	// A download that fails (or stalls) goes on from where it got to, this many times in all, before the read fails.
	private const int MaxAttempts = 3;
	private static readonly TimeSpan s_defaultReadTimeout = TimeSpan.FromSeconds(20);

	private static readonly HttpClient s_client = new();

	private readonly Func<long, CancellationToken, Task<Stream>> _open;
	private readonly Window[] _windows;
	private readonly long _keepBehind;
	// A read this far past what a window has downloaded is a seek, not worth waiting for.
	private readonly long _maxWait;
	// How long a download may go without getting anything before it's taken to have stalled (HttpClient's own timeout
	// stops at the response's headers).
	private readonly TimeSpan _readTimeout;
	private readonly object _gate = new();
	private long _position;
	private long _uses;
	private bool _isDisposed;
	// A Matroska file's subtitles, read as it downloads: what its header says, and those read so far (each once).
	private readonly MatroskaHeader? _matroska;
	private readonly List<MatroskaCue> _cues = [];
	private readonly HashSet<MatroskaCue> _cuesRead = [];
	private Action<MatroskaCue>? _onCue;

	/// <param name="open">The file's body from an offset to its end.</param>
	/// <param name="capacity">How much is kept in memory, across the windows.</param>
	/// <param name="first">The body from the start, if it's already been asked for.</param>
	/// <param name="readSubtitles">It's a Matroska file whose subtitles are read as it downloads.</param>
	/// <param name="readTimeout">How long a download may get nothing before it's started again from where it got to.</param>
	internal ReadAheadStream(long length, Func<long, CancellationToken, Task<Stream>> open, int capacity = DefaultCapacity, Stream? first = null, bool readSubtitles = false,
		TimeSpan? readTimeout = null)
	{
		Length = length;
		_open = open;
		_readTimeout = readTimeout ?? s_defaultReadTimeout;
		_matroska = readSubtitles ? new MatroskaHeader() : null;
		var size = (int)Math.Max(1, Math.Min(capacity / WindowCount, length));
		_windows = [.. Enumerable.Range(0, WindowCount).Select(_ => new Window(size))];
		_keepBehind = size / 4;
		_maxWait = size / 8;
		_windows[0].IsStart = true;
		Restart(_windows[0], 0, first);
	}

	/// <summary>
	/// The file at <paramref name="uri"/> and its content type; null if it can't be read from any offset (or it's a
	/// playlist), so it's played from its URL instead.
	/// </summary>
	public static async Task<(ReadAheadStream Stream, string ContentType)?> OpenAsync(Uri uri, CancellationToken ct = default)
	{
		HttpResponseMessage response;
		try
		{
			response = await SendAsync(uri, 0, ct);
		}
		catch (HttpRequestException)
		{
			return null;
		}

		var type = response.Content.Headers.ContentType?.MediaType ?? "";
		// Asked for all of it, a host may answer with all of it (200) rather than a range, saying it takes ranges.
		var length = response.StatusCode == HttpStatusCode.PartialContent ? response.Content.Headers.ContentRange?.Length
			: response.StatusCode == HttpStatusCode.OK && response.Headers.AcceptRanges.Contains("bytes") ? response.Content.Headers.ContentLength
			: null;
		if (length is null ||
			type.Contains("mpegurl", StringComparison.OrdinalIgnoreCase) || type.Contains("dash", StringComparison.OrdinalIgnoreCase))
		{
			response.Dispose();
			return null;
		}

		var body = await response.Content.ReadAsStreamAsync(ct);
		type = type.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? type : GuessType(response.RequestMessage?.RequestUri ?? uri);
		var stream = new ReadAheadStream(length.Value, (offset, token) => OpenAtAsync(uri, offset, token), first: body,
			readSubtitles: type.Equals("video/x-matroska", StringComparison.OrdinalIgnoreCase));
		return (stream, type);
	}

	/// <summary>
	/// A Matroska file's subtitle tracks, read from its header (none if it isn't one, or has none). Its subtitles are read
	/// as it downloads, for <see cref="OnSubtitle"/>.
	/// </summary>
	public Task<IReadOnlyList<MatroskaSubtitleTrack>> SubtitleTracks => _matroska?.Tracks.Task ?? Task.FromResult<IReadOnlyList<MatroskaSubtitleTrack>>([]);

	/// <summary>Has <paramref name="onCue"/> called with each subtitle read, those read so far first (on whatever thread).</summary>
	public void OnSubtitle(Action<MatroskaCue> onCue)
	{
		MatroskaCue[] read;
		lock (_gate)
		{
			read = [.. _cues];
			_onCue = onCue;
		}

		foreach (var cue in read)
		{
			onCue(cue);
		}
	}

	public override long Length { get; }

	public override bool CanRead => true;

	public override bool CanSeek => true;

	public override bool CanWrite => false;

	public override long Position
	{
		get
		{
			lock (_gate)
			{
				return _position;
			}
		}
		set
		{
			ArgumentOutOfRangeException.ThrowIfNegative(value);
			lock (_gate)
			{
				_position = value;
			}
		}
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		return Position = origin switch
		{
			SeekOrigin.Begin => offset,
			SeekOrigin.Current => Position + offset,
			SeekOrigin.End => Length + offset,
			_ => throw new ArgumentOutOfRangeException(nameof(origin)),
		};
	}

	public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

	// Fills the buffer unless the file ends first, as reading a file would.
	public override int Read(Span<byte> buffer)
	{
		var total = 0;
		while (total < buffer.Length && ReadSome(buffer[total..]) is > 0 and var read)
		{
			total += read;
		}

		return total;
	}

	private int ReadSome(Span<byte> buffer)
	{
		lock (_gate)
		{
			while (true)
			{
				ObjectDisposedException.ThrowIf(_isDisposed, this);
				if (buffer.Length == 0 || _position >= Length)
				{
					return 0;
				}

				if (Array.Find(_windows, x => x.Download is not null && _position >= x.Start && _position < x.End) is { } have)
				{
					var count = (int)Math.Min(buffer.Length, have.End - _position);
					var at = (int)(_position % have.Ring.Length);
					var first = Math.Min(count, have.Ring.Length - at);
					have.Ring.AsSpan(at, first).CopyTo(buffer);
					have.Ring.AsSpan(0, count - first).CopyTo(buffer[first..]);
					_position += count;
					// The furthest it's been read: going back a little (as the reader does) doesn't keep what's behind.
					have.ReadTo = Math.Max(have.ReadTo, _position);
					have.LastUsed = ++_uses;
					// There may be room for its download now.
					Monitor.PulseAll(_gate);
					return count;
				}

				var window = Array.Find(_windows, x => x.Download is not null && !(x.IsStart && x.IsFull) && _position >= x.End && _position <= x.End + _maxWait);
				if (window is null)
				{
					window = _windows.Where(x => !x.IsStart).MinBy(x => x.LastUsed)!;
					Restart(window, _position, null);
				}
				else if (window.Error is { } error)
				{
					throw new IOException("Couldn't download the video.", error);
				}

				// What it has before here can go to make room, so its download (waiting for room) goes on.
				window.ReadTo = Math.Max(window.ReadTo, _position);
				window.LastUsed = ++_uses;
				Monitor.PulseAll(_gate);
				Monitor.Wait(_gate);
			}
		}
	}

	public override void Flush()
	{
	}

	public override void SetLength(long value) => throw new NotSupportedException();

	public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			lock (_gate)
			{
				_isDisposed = true;
				foreach (var window in _windows)
				{
					window.Download?.Cancel();
				}

				Monitor.PulseAll(_gate);
			}
		}

		base.Dispose(disposing);
	}


	/// <summary>Has <paramref name="window"/> download from <paramref name="from"/> instead (under the lock, or before it's shared).</summary>
	private void Restart(Window window, long from, Stream? body)
	{
		window.Download?.Cancel();
		window.Download = new CancellationTokenSource();
		window.Start = window.End = window.ReadTo = from;
		window.Error = null;
		// Going on from where the start's window stopped (it's full), its reader goes on too: a new one would only find its
		// place from the next cluster, missing the rest of that one's subtitles.
		var start = Array.Find(_windows, x => x.IsStart && x != window && x.IsFull && x.End == from && x.Subtitles is not null);
		window.Subtitles = start?.Subtitles ?? (_matroska is null ? null : new MatroskaSubtitleParser(_matroska, from));
		if (start is not null)
		{
			start.Subtitles = null;
		}
		var token = window.Download.Token;
		_ = Task.Run(() => DownloadAsync(window, from, body, token));
	}

	/// <summary>Reads the subtitles in what <paramref name="window"/> has downloaded next, adding those not read before (under the lock).</summary>
	private void ReadSubtitles(Window window, ReadOnlySpan<byte> downloaded, List<MatroskaCue> cues)
	{
		if (window.Subtitles is not { } parser)
		{
			return;
		}

		var count = cues.Count;
		try
		{
			parser.Feed(downloaded, cues);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or OverflowException)
		{
			// A file it can't make sense of: its subtitles aren't read here any more (the video plays on).
			window.Subtitles = null;
		}

		// The same part of the file may be downloaded again.
		for (var i = cues.Count - 1; i >= count; i--)
		{
			if (!_cuesRead.Add(cues[i]))
			{
				cues.RemoveAt(i);
			}
		}
	}

	/// <summary>Passes on the subtitles read (outside the lock), and keeps them for whoever asks later.</summary>
	private void Notify(List<MatroskaCue> cues)
	{
		Action<MatroskaCue>? onCue;
		lock (_gate)
		{
			_cues.AddRange(cues);
			onCue = _onCue;
		}

		foreach (var cue in cues)
		{
			onCue?.Invoke(cue);
		}

		cues.Clear();
	}

	private async Task DownloadAsync(Window window, long from, Stream? body, CancellationToken ct)
	{
		var chunk = new byte[64 * 1024];
		List<MatroskaCue> cues = [];
		for (var attempt = 1; ; attempt++)
		{
			try
			{
				if (attempt > 1)
				{
					// On from what the window has (it's contiguous, so its subtitles' reader goes on too). One that got
					// somewhere before failing starts its count again: hiccups far apart don't add up.
					lock (_gate)
					{
						if (window.End > from)
						{
							attempt = 2;
						}

						from = window.End;
					}

					// A moment for the connection to come back.
					await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt - 1)), ct);
				}

				body ??= await _open(from, ct);
				await using (body)
				{
					await DownloadIntoAsync(window, from, body, chunk, cues, ct);
				}

				return;
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				return;
			}
			catch (Exception) when (attempt < MaxAttempts)
			{
				body = null;
			}
			catch (Exception ex)
			{
				lock (_gate)
				{
					if (!ct.IsCancellationRequested)
					{
						window.Error = ex;
						Monitor.PulseAll(_gate);
					}
				}

				return;
			}
			finally
			{
				if (cues.Count > 0)
				{
					Notify(cues);
				}
			}
		}
	}

	/// <summary>Downloads <paramref name="body"/> (from <paramref name="from"/>) into <paramref name="window"/> as there's room.</summary>
	private async Task DownloadIntoAsync(Window window, long from, Stream body, byte[] chunk, List<MatroskaCue> cues, CancellationToken ct)
	{
		using var stalled = CancellationTokenSource.CreateLinkedTokenSource(ct);
		while (true)
		{
			if (cues.Count > 0)
			{
				Notify(cues);
			}

			// Timed only while reading: waiting for room (the reader not reading on) isn't a stall.
			stalled.CancelAfter(_readTimeout);
			int read;
			try
			{
				read = await body.ReadAsync(chunk, stalled.Token);
			}
			catch (OperationCanceledException) when (!ct.IsCancellationRequested)
			{
				throw new TimeoutException($"The video's download got nothing for {_readTimeout.TotalSeconds:0} seconds.");
			}

			stalled.CancelAfter(Timeout.InfiniteTimeSpan);
			if (read == 0)
			{
				if (from < Length)
				{
					throw new IOException($"The video's download ended at {from} of {Length}.");
				}

				return;
			}

			lock (_gate)
			{
				var ring = window.Ring;
				var copied = 0;
				while (copied < read)
				{
					if (ct.IsCancellationRequested)
					{
						return;
					}

					// The start's window stops once it's full; another makes room, once it's full, by forgetting what's
					// well behind where it was read.
					if (window.IsFull && window.IsStart)
					{
						return;
					}

					if (window.IsFull)
					{
						window.Start = Math.Max(window.Start, Math.Min(window.End, window.ReadTo - _keepBehind));
					}

					var room = ring.Length - (int)(window.End - window.Start);
					if (room == 0)
					{
						Monitor.Wait(_gate);
						continue;
					}

					var count = Math.Min(room, read - copied);
					var at = (int)(window.End % ring.Length);
					var first = Math.Min(count, ring.Length - at);
					chunk.AsSpan(copied, first).CopyTo(ring.AsSpan(at));
					chunk.AsSpan(copied + first, count - first).CopyTo(ring);
					ReadSubtitles(window, chunk.AsSpan(copied, count), cues);
					window.End += count;
					from += count;
					copied += count;
					Monitor.PulseAll(_gate);
				}
			}
		}
	}

	private static async Task<Stream> OpenAtAsync(Uri uri, long offset, CancellationToken ct)
	{
		var response = await SendAsync(uri, offset, ct);
		// From the start, the host may answer with all of it (200), as when it was first asked for.
		if (response.StatusCode != HttpStatusCode.PartialContent && !(offset == 0 && response.StatusCode == HttpStatusCode.OK))
		{
			response.Dispose();
			throw new IOException($"Asked for the video from {offset}, the host answered {(int)response.StatusCode}.");
		}

		return await response.Content.ReadAsStreamAsync(ct);
	}

	private static Task<HttpResponseMessage> SendAsync(Uri uri, long offset, CancellationToken ct)
	{
		var request = new HttpRequestMessage(HttpMethod.Get, uri) { Headers = { Range = new RangeHeaderValue(offset, null) } };
		return s_client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
	}

	// Hosts often say only application/octet-stream; Windows needs to know the container.
	private static string GuessType(Uri uri)
	{
		return Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() switch
		{
			".mkv" => "video/x-matroska",
			".webm" => "video/webm",
			_ => "video/mp4",
		};
	}

	/// <summary>Part of the file downloaded, [Start, End), each byte at its offset % its size in Ring.</summary>
	private sealed class Window(int size)
	{
		// Made as it's first used (under the lock): a short video, or one played straight through, needs only one or two.
		public byte[] Ring => field ??= new byte[size];
		public long Start;
		public long End;
		// Where it was last read up to: what's well behind it can go.
		public long ReadTo;
		public long LastUsed;
		public Exception? Error;
		// Null while it's never been used.
		public CancellationTokenSource? Download;
		// The file's start, kept: never started again elsewhere, nor made room in.
		public bool IsStart;
		// Reads the subtitles of a Matroska file in what it downloads.
		public MatroskaSubtitleParser? Subtitles;

		public bool IsFull => End - Start == size;
	}
}
