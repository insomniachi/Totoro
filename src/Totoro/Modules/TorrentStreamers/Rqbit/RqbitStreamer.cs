using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Totoro.Core.Helpers;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;
using Totoro.Helpers;

namespace Totoro.Modules.TorrentStreamers.Rqbit;

/// <summary>
/// Streams torrents with rqbit (https://github.com/ikatson/rqbit), run as a local server for the app's lifetime and
/// driven over its HTTP API: torrents are added to it, and players read a file from its stream URL, which waits for the
/// pieces being read and fetches them first. What's streamed goes to the Torrents folder in the app's data, and is
/// deleted when the stream ends unless the settings keep it; an episode downloaded goes to the folder it's for.
/// </summary>
internal sealed class RqbitStreamer(IModuleSettings<Settings> settings, RqbitInstaller installer, ILogger<RqbitStreamer> logger) : ITorrentStreamer, IDisposable
{
	private static readonly TimeSpan s_startTimeout = TimeSpan.FromSeconds(15);
	private static readonly TimeSpan s_pollInterval = TimeSpan.FromMilliseconds(500);

	private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
	private readonly SemaphoreSlim _startLock = new(1, 1);
	private readonly Lock _streamsLock = new();

	// How many streams use each torrent (by rqbit's id), so ending one doesn't delete a torrent another still plays,
	// e.g. when the same episode is started again.
	private readonly Dictionary<int, int> _streams = [];

	private Process? _process;
	private Uri? _api;

	public Guid Id => RqbitModule.Id;

	public string Name => "rqbit";

	/// <summary>Where torrents are downloaded to.</summary>
	public static string DownloadFolder => FileHelper.GetPath("Torrents");

	// Always: rqbit is downloaded the first time it's needed (see StartAsync).
	public bool IsAvailable() => true;

	public async Task<TorrentStream> StreamAsync(Uri torrent, Func<IReadOnlyList<TorrentFile>, TorrentFile?> selectFile, IProgress<string>? progress,
												 CancellationToken ct)
	{
		var api = await StartAsync(ct);

		progress?.Report("Getting the torrent…");
		// Adding a torrent that's already there returns it; overwrite lets it use files already downloaded.
		var (content, isUrl) = await GetTorrentAsync(torrent, ct);
		var added = await SendAsync(HttpMethod.Post, new Uri(api, $"torrents?overwrite=true&is_url={(isUrl ? "true" : "false")}"), content, ct);
		var id = added["id"]!.GetValue<int>();
		lock (_streamsLock)
		{
			_streams[id] = _streams.GetValueOrDefault(id) + 1;
		}

		try
		{
			var file = await SelectFileAsync(api, id, added, selectFile, progress, ct);
			return new TorrentStream(new Uri(api, $"torrents/{id}/stream/{file.Index}"), file, () => new ValueTask(StopAsync(api, id)));
		}
		catch
		{
			await StopAsync(api, id);
			throw;
		}
	}

	public async Task<string> DownloadAsync(Uri torrent, Func<IReadOnlyList<TorrentFile>, TorrentFile?> selectFile, string folder, IProgress<string>? status,
											IProgress<TorrentDownloadProgress> progress, CancellationToken ct)
	{
		var api = await StartAsync(ct);

		status?.Report("Getting the torrent…");
		var (content, isUrl) = await GetTorrentAsync(torrent, ct);
		Directory.CreateDirectory(folder);
		var added = await SendAsync(HttpMethod.Post,
									new Uri(api, $"torrents?overwrite=true&is_url={(isUrl ? "true" : "false")}&output_folder={Uri.EscapeDataString(folder)}"), content, ct);
		var id = added["id"]!.GetValue<int>();
		// shortcut: a torrent rqbit already has (being watched) stays in its own folder, so it isn't downloaded meanwhile; share it if that comes up.
		if ((added["output_folder"] ?? added["details"]?["output_folder"])?.GetValue<string>() is { } output &&
			!Path.GetFullPath(output).TrimEnd('\\').Equals(Path.GetFullPath(folder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
		{
			throw new TorrentStreamException("It's playing: download it once it's stopped.");
		}

		lock (_streamsLock)
		{
			_streams[id] = _streams.GetValueOrDefault(id) + 1;
		}

		var keep = false;
		try
		{
			var file = await SelectFileAsync(api, id, added, selectFile, status, ct);
			while (true)
			{
				var stats = await SendAsync(HttpMethod.Get, new Uri(api, $"torrents/{id}/stats/v1"), null, ct);
				if (stats["state"]?.GetValue<string>() == "error")
				{
					throw new TorrentStreamException($"rqbit couldn't download it: {stats["error"]}");
				}

				var done = stats["file_progress"]?[file.Index]?.GetValue<long>() ?? 0;
				// rqbit's "mbps" is MiB a second.
				var speed = stats["live"]?["download_speed"]?["mbps"]?.GetValue<double>() * (1 << 20);
				progress.Report(new TorrentDownloadProgress(done, file.Length, speed));
				if (done >= file.Length)
				{
					break;
				}

				await Task.Delay(TimeSpan.FromSeconds(1), ct);
			}

			keep = true;
			// Where rqbit put it: in the folder, under the torrent's own folders if it has any.
			return Directory.EnumerateFiles(folder, Path.GetFileName(file.Path), SearchOption.AllDirectories).FirstOrDefault()
				   ?? throw new TorrentStreamException("rqbit finished, but the file isn't in the folder it was downloaded to.");
		}
		finally
		{
			// Forgotten once it's downloaded, so its file is the caller's; otherwise deleted with what was downloaded.
			await StopAsync(api, id, keep);
		}
	}

	/// <summary>
	/// The file of the torrent rqbit added (its <paramref name="added"/> response) that <paramref name="selectFile"/>
	/// picks, once the torrent's live and set to download only that file.
	/// </summary>
	private async Task<TorrentFile> SelectFileAsync(Uri api, int id, JsonNode added, Func<IReadOnlyList<TorrentFile>, TorrentFile?> selectFile,
													IProgress<string>? progress, CancellationToken ct)
	{
		var files = added["details"]!["files"]!.AsArray()
											  .Select((x, i) => (File: x!, Index: i))
											  .Where(x => x.File["attributes"]?["padding"]?.GetValue<bool>() != true)
											  .Select(x => new TorrentFile(x.Index, string.Join('/', x.File["components"]!.AsArray().Select(c => c!.GetValue<string>())),
																		   x.File["length"]!.GetValue<long>()))
											  .ToList();
		if (selectFile(files) is not { } file)
		{
			throw new TorrentStreamException("The torrent has no video to play.");
		}

		await WaitUntilLiveAsync(api, id, progress, ct);

		// Only the file wanted; files can only be chosen once the torrent is live.
		if (files.Count > 1)
		{
			await SendAsync(HttpMethod.Post, new Uri(api, $"torrents/{id}/update_only_files"), JsonContent.Create(new { only_files = new[] { file.Index } }), ct);
		}

		return file;
	}

	public void Dispose()
	{
		// Also ended by Windows when the app exits (see ChildProcesses); this is for an orderly shutdown.
		try
		{
			_process?.Kill();
		}
		catch (InvalidOperationException)
		{
		}

		_http.Dispose();
	}

	/// <summary>
	/// What to give rqbit for the torrent: a magnet link as it is, or a .torrent file's contents, downloaded here. rqbit
	/// could download it itself, but it takes any 40-character text for an info hash, and nyaa.si's links
	/// (https://nyaa.si/download/1234567.torrent) are 40 characters.
	/// </summary>
	private async Task<(HttpContent Content, bool IsUrl)> GetTorrentAsync(Uri torrent, CancellationToken ct)
	{
		if (torrent.Scheme is not ("http" or "https"))
		{
			return (new StringContent(torrent.AbsoluteUri), true);
		}

		try
		{
			return (new ByteArrayContent(await _http.GetByteArrayAsync(torrent, ct)), false);
		}
		catch (HttpRequestException ex)
		{
			throw new TorrentStreamException($"Couldn't download the torrent from {torrent.Host}: {ex.Message}", ex);
		}
	}

	/// <summary>Waits until rqbit can stream the torrent (it refuses to while it's still "initializing").</summary>
	private async Task WaitUntilLiveAsync(Uri api, int id, IProgress<string>? progress, CancellationToken ct)
	{
		while (true)
		{
			var stats = await SendAsync(HttpMethod.Get, new Uri(api, $"torrents/{id}/stats/v1"), null, ct);
			switch (stats["state"]?.GetValue<string>())
			{
				case "live":
					return;
				case "error":
					throw new TorrentStreamException($"rqbit couldn't stream it: {stats["error"]}");
			}

			var peers = stats["live"]?["snapshot"]?["peer_stats"]?["live"]?.GetValue<int>();
			progress?.Report(peers is > 0 ? $"Connecting to peers… {peers} connected" : "Connecting to peers…");
			await Task.Delay(s_pollInterval, ct);
		}
	}

	/// <summary>
	/// Ends a stream (or download): deletes the torrent once no stream uses it, with its files unless
	/// <paramref name="keepFiles"/> (by default, the settings) keeps them.
	/// </summary>
	private async Task StopAsync(Uri api, int id, bool? keepFiles = null)
	{
		lock (_streamsLock)
		{
			var count = _streams.GetValueOrDefault(id) - 1;
			if (count > 0)
			{
				_streams[id] = count;
				return;
			}

			_streams.Remove(id);
		}

		try
		{
			// "forget" stops it and keeps the files; "delete" removes them too.
			var action = keepFiles ?? settings.Value.KeepDownloads ? "forget" : "delete";
			await SendAsync(HttpMethod.Post, new Uri(api, $"torrents/{id}/{action}"), null, CancellationToken.None);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't remove torrent {Id} from rqbit", id);
		}
	}

	/// <summary>Starts the rqbit server, if it isn't running; returns its API's address.</summary>
	private async Task<Uri> StartAsync(CancellationToken ct)
	{
		await _startLock.WaitAsync(ct);
		try
		{
			if (_process is { HasExited: false } && _api is not null)
			{
				return _api;
			}

			if (!RqbitInstaller.IsInstalled)
			{
				await installer.InstallAsync(ct);
				if (!RqbitInstaller.IsInstalled)
				{
					throw new TorrentStreamException(installer.Error is { Length: > 0 } error ? error : "rqbit is downloading: try again in a moment.");
				}
			}

			if (!settings.Value.KeepDownloads)
			{
				// Left over from streams that didn't end normally (e.g. the app was closed while playing).
				DeleteDownloads();
			}

			Directory.CreateDirectory(DownloadFolder);
			var api = new Uri($"http://127.0.0.1:{GetFreePort()}/");
			var process = new Process
			{
				StartInfo = new ProcessStartInfo(RqbitInstaller.Exe)
				{
					ArgumentList =
					{
						"--http-api-listen-addr", $"{api.Host}:{api.Port}",
						// Nothing is resumed from a previous run: what's streamed is added again when it's played.
						"server", "start", "--disable-persistence", DownloadFolder,
					},
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				},
			};
			process.OutputDataReceived += (_, e) => Log(e.Data);
			process.ErrorDataReceived += (_, e) => Log(e.Data);
			process.Start();
			ChildProcesses.Add(process);
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();

			// Ready once its API answers.
			var started = Stopwatch.StartNew();
			while (true)
			{
				if (process.HasExited)
				{
					throw new TorrentStreamException($"rqbit stopped as it started (exit code {process.ExitCode}).");
				}

				try
				{
					using var response = await _http.GetAsync(api, ct);
					if (response.IsSuccessStatusCode)
					{
						break;
					}
				}
				catch (HttpRequestException) when (started.Elapsed < s_startTimeout)
				{
				}

				if (started.Elapsed >= s_startTimeout)
				{
					process.Kill();
					throw new TorrentStreamException("rqbit didn't start.");
				}

				await Task.Delay(100, ct);
			}

			_process = process;
			_api = api;
			return api;
		}
		finally
		{
			_startLock.Release();
		}
	}

	/// <summary>Calls rqbit's API; its errors become <see cref="TorrentStreamException"/>s with rqbit's explanation.</summary>
	private async Task<JsonNode> SendAsync(HttpMethod method, Uri url, HttpContent? content, CancellationToken ct)
	{
		using var request = new HttpRequestMessage(method, url) { Content = content };
		using var response = await _http.SendAsync(request, ct);
		var body = await response.Content.ReadAsStringAsync(ct);
		var json = string.IsNullOrWhiteSpace(body) ? new JsonObject() : JsonNode.Parse(body) ?? new JsonObject();
		if (!response.IsSuccessStatusCode)
		{
			var reason = json["human_readable"]?.GetValue<string>() ?? $"{(int)response.StatusCode} {response.ReasonPhrase}";
			throw new TorrentStreamException($"rqbit couldn't stream it: {reason}");
		}

		return json;
	}

	private void Log(string? line)
	{
		if (!string.IsNullOrWhiteSpace(line))
		{
			logger.LogDebug("rqbit: {Line}", line);
		}
	}

	private void DeleteDownloads()
	{
		try
		{
			if (Directory.Exists(DownloadFolder))
			{
				Directory.Delete(DownloadFolder, recursive: true);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Couldn't delete old torrent downloads");
		}
	}

	private static int GetFreePort()
	{
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		return ((IPEndPoint)listener.LocalEndpoint).Port;
	}
}
