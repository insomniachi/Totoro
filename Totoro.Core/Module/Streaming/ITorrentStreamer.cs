namespace Totoro.Core.Module.Streaming;

/// <summary>
/// Turns a torrent into a video a media player can open over HTTP(S): by streaming it locally (rqbit), or by having a
/// debrid service (TorBox, Real-Debrid...) fetch it and linking to its copy. Modules register implementations keyed by
/// their module id.
/// </summary>
public interface ITorrentStreamer
{
	/// <summary>The module's id.</summary>
	Guid Id { get; }

	string Name { get; }

	/// <summary>Whether it's set up to stream, e.g. its program is there, or an account is signed in.</summary>
	bool IsAvailable();

	/// <summary>
	/// Starts streaming a file of the torrent at <paramref name="torrent"/> (a .torrent URL or a magnet link): the one
	/// <paramref name="selectFile"/> picks from its files. Reports what it's doing (for a status bar) until a player
	/// can open the stream, then returns it. Dispose it when playback ends, to stop streaming and clean up.
	/// </summary>
	/// <exception cref="TorrentStreamException">The torrent couldn't be streamed; the message says why.</exception>
	Task<TorrentStream> StreamAsync(Uri torrent, Func<IReadOnlyList<TorrentFile>, TorrentFile?> selectFile, IProgress<string>? progress,
									CancellationToken ct);
}

/// <param name="Index">Its position in the torrent's file list.</param>
/// <param name="Path">Its path in the torrent, with / between folders.</param>
public sealed record TorrentFile(int Index, string Path, long Length)
{
	private static readonly string[] s_videoExtensions = [".mkv", ".mp4", ".avi", ".webm", ".m4v", ".mov", ".ts", ".wmv"];

	public bool IsVideo => s_videoExtensions.Contains(System.IO.Path.GetExtension(Path), StringComparer.OrdinalIgnoreCase);

	/// <summary>The largest video file, which for an episode's torrent is the episode (rather than e.g. a sample).</summary>
	public static TorrentFile? LargestVideo(IReadOnlyList<TorrentFile> files)
	{
		return files.Where(x => x.IsVideo).MaxBy(x => x.Length);
	}
}

/// <summary>
/// A file being streamed, at <see cref="Url"/>. Disposing it stops streaming (and, for a local stream, deletes what was
/// downloaded, unless the streamer keeps downloads).
/// </summary>
public sealed class TorrentStream(Uri url, TorrentFile file, Func<ValueTask>? stop = null) : IAsyncDisposable
{
	private Func<ValueTask>? _stop = stop;

	public Uri Url { get; } = url;

	public TorrentFile File { get; } = file;

	/// <summary>HTTP headers the player has to send with requests for the video, e.g. a debrid service's.</summary>
	public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

	public ValueTask DisposeAsync()
	{
		// Once only.
		return Interlocked.Exchange(ref _stop, null)?.Invoke() ?? ValueTask.CompletedTask;
	}
}

/// <summary>A torrent couldn't be streamed; the message says why, in words to show the user.</summary>
public sealed class TorrentStreamException(string message, Exception? innerException = null) : Exception(message, innerException);
