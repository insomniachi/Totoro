using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Totoro.Modules.MediaPlayers.Vlc;

/// <summary>
/// Serves videos on localhost, fetching them with HTTP headers VLC can't send itself (it only sends a user agent and a
/// referrer): VLC plays the local URL, and each request (with its range, for seeking) is passed on with the headers. An
/// HLS playlist's own URLs are rewritten to go through it too, so its segments get the headers.
/// </summary>
internal sealed partial class HeaderProxy(ILogger logger) : IDisposable
{
	private static readonly HttpClient s_client = new(new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.None });

	// What each local path fetches.
	private readonly ConcurrentDictionary<string, Target> _targets = new();
	private TcpListener? _listener;
	private CancellationTokenSource? _stopping;

	private sealed record Target(Uri Uri, IReadOnlyDictionary<string, string> Headers);

	/// <summary>A local URL that fetches <paramref name="uri"/> with <paramref name="headers"/>.</summary>
	public Uri Serve(Uri uri, IReadOnlyDictionary<string, string> headers)
	{
		var port = Start();
		var id = Guid.NewGuid().ToString("N");
		_targets[id] = new Target(uri, headers);
		// The file name stays at the end, so VLC can tell the format from it.
		return new Uri($"http://127.0.0.1:{port}/{id}/{Uri.EscapeDataString(Path.GetFileName(uri.AbsolutePath))}");
	}

	/// <summary>Forgets the URLs served so far (what was playing).</summary>
	public void Clear() => _targets.Clear();

	public void Dispose()
	{
		_stopping?.Cancel();
		_listener?.Stop();
	}

	private int Start()
	{
		if (_listener is null)
		{
			_listener = new TcpListener(IPAddress.Loopback, 0);
			_listener.Start();
			_stopping = new CancellationTokenSource();
			_ = AcceptAsync(_listener, _stopping.Token);
		}

		return ((IPEndPoint)_listener.LocalEndpoint).Port;
	}

	private async Task AcceptAsync(TcpListener listener, CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				var client = await listener.AcceptTcpClientAsync(ct);
				_ = HandleAsync(client, ct);
			}
			catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
			{
				return;
			}
		}
	}

	/// <summary>Answers one request (and closes the connection, which VLC is fine with).</summary>
	private async Task HandleAsync(TcpClient client, CancellationToken ct)
	{
		using var _ = client;
		try
		{
			var stream = client.GetStream();
			if (await ReadRequestAsync(stream, ct) is not var (method, path, range) ||
				path.Split('/', StringSplitOptions.RemoveEmptyEntries) is not [var id, ..] ||
				!_targets.TryGetValue(id, out var target))
			{
				await WriteHeadAsync(stream, HttpStatusCode.NotFound, [], ct);
				return;
			}

			using var request = new HttpRequestMessage(method == "HEAD" ? HttpMethod.Head : HttpMethod.Get, target.Uri);
			foreach (var (name, value) in target.Headers)
			{
				if (!request.Headers.TryAddWithoutValidation(name, value))
				{
					logger.LogDebug("Can't send the header {Header}", name);
				}
			}

			if (range is not null)
			{
				request.Headers.TryAddWithoutValidation("Range", range);
			}

			using var response = await s_client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
			var type = response.Content.Headers.ContentType?.MediaType ?? "";
			if (IsPlaylist(type, target.Uri))
			{
				// Read whole and rewritten, so it has no range or length of its own.
				var playlist = Encoding.UTF8.GetBytes(RewritePlaylist(await response.Content.ReadAsStringAsync(ct), response.RequestMessage?.RequestUri ?? target.Uri, target.Headers));
				await WriteHeadAsync(stream, response.StatusCode, [("Content-Type", type), ("Content-Length", playlist.Length.ToString())], ct);
				if (method != "HEAD")
				{
					await stream.WriteAsync(playlist, ct);
				}

				return;
			}

			List<(string, string)> headers = [];
			foreach (var name in new[] { "Content-Type", "Content-Length", "Content-Range" })
			{
				if (response.Content.Headers.TryGetValues(name, out var values))
				{
					headers.Add((name, string.Join(", ", values)));
				}
			}

			headers.Add(("Accept-Ranges", response.Headers.AcceptRanges.Count > 0 ? string.Join(", ", response.Headers.AcceptRanges) : "bytes"));
			await WriteHeadAsync(stream, response.StatusCode, headers, ct);
			if (method != "HEAD")
			{
				await using var body = await response.Content.ReadAsStreamAsync(ct);
				await body.CopyToAsync(stream, ct);
			}
		}
		catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException or ObjectDisposedException)
		{
			// VLC hung up (it does when seeking), or the proxy is stopping.
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't pass on a request for the video");
		}
	}

	/// <summary>The request line's method and path, and its Range header; null if it isn't a request.</summary>
	private static async Task<(string Method, string Path, string? Range)?> ReadRequestAsync(NetworkStream stream, CancellationToken ct)
	{
		var head = new StringBuilder();
		var buffer = new byte[1];
		// Byte by byte, so nothing past the head is read; a request's head is small.
		while (head.Length < 16 * 1024 && !head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
		{
			if (await stream.ReadAsync(buffer, ct) == 0)
			{
				return null;
			}

			head.Append((char)buffer[0]);
		}

		var lines = head.ToString().Split("\r\n");
		if (lines[0].Split(' ') is not [var method, var path, ..])
		{
			return null;
		}

		var range = lines.Skip(1).FirstOrDefault(x => x.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))?["Range:".Length..].Trim();
		return (method, path, range);
	}

	private static async Task WriteHeadAsync(NetworkStream stream, HttpStatusCode status, IEnumerable<(string Name, string Value)> headers, CancellationToken ct)
	{
		var head = new StringBuilder($"HTTP/1.1 {(int)status} {status}\r\n");
		foreach (var (name, value) in headers)
		{
			head.Append($"{name}: {value}\r\n");
		}

		head.Append("Connection: close\r\n\r\n");
		await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString()), ct);
	}

	private static bool IsPlaylist(string mediaType, Uri uri)
	{
		return mediaType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase) ||
			   uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>The HLS playlist with its URLs (segments, variant playlists, keys) served by the proxy too.</summary>
	private string RewritePlaylist(string playlist, Uri baseUri, IReadOnlyDictionary<string, string> headers)
	{
		string Local(string url) => Serve(new Uri(baseUri, url), headers).AbsoluteUri;

		var lines = playlist.Split('\n');
		for (var i = 0; i < lines.Length; i++)
		{
			var line = lines[i].TrimEnd('\r');
			if (line.Length == 0)
			{
				continue;
			}

			lines[i] = line.StartsWith('#')
				? UriAttribute().Replace(line, x => $"URI=\"{Local(x.Groups[1].Value)}\"")
				: Local(line);
		}

		return string.Join('\n', lines);
	}

	[GeneratedRegex("URI=\"([^\"]+)\"")]
	private static partial Regex UriAttribute();
}
