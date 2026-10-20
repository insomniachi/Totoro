using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Helpers;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Relations;
using Totoro.Core.Settings;
using Totoro.Helpers;

namespace Totoro.Playback;

public enum DownloadState
{
	Downloading,
	Completed,
	Failed,
	Cancelled,
}

/// <summary>An episode being downloaded, or that was.</summary>
public sealed partial class DownloadItem : ObservableObject
{
	private readonly CancellationTokenSource _cancellation = new();

	/// <summary>e.g. "Sousou no Frieren · Episode 5".</summary>
	public required string Title { get; init; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsActive))]
	public partial DownloadState State { get; internal set; }

	/// <summary>How much is downloaded, from 0 to 1; -1 while that isn't known.</summary>
	[ObservableProperty] public partial double Progress { get; internal set; } = -1;

	/// <summary>What it's doing, or why it failed.</summary>
	[ObservableProperty] public partial string Message { get; internal set; } = "";

	/// <summary>The episode's file once it's downloaded; empty, never null, until then.</summary>
	[ObservableProperty] public partial string FilePath { get; internal set; } = "";

	/// <summary>The anime's folder (see <see cref="DownloadNames"/>).</summary>
	public required string Folder { get; init; }

	public bool IsActive => State == DownloadState.Downloading;

	internal CancellationToken Token => _cancellation.Token;

	public void Cancel() => _cancellation.Cancel();

	/// <summary>Shows the episode's file (or, until it's downloaded, the anime's folder) in File Explorer.</summary>
	public void ShowInExplorer()
	{
		if (File.Exists(FilePath))
		{
			Process.Start("explorer.exe", $"/select,\"{FilePath}\"");
		}
		else if (Directory.Exists(Folder))
		{
			Process.Start("explorer.exe", $"\"{Folder}\"");
		}
	}
}

/// <param name="Fraction">How much is downloaded, from 0 to 1; -1 when the size isn't known.</param>
/// <param name="BytesPerSecond">How fast it's going lately; null when that isn't known.</param>
/// <param name="DownloadedBytes">How much is downloaded; null when that isn't known.</param>
/// <param name="TotalBytes">The whole size, or an estimate of it; null when that isn't known.</param>
internal readonly record struct DownloadProgress(double Fraction, double? BytesPerSecond, double? DownloadedBytes = null, double? TotalBytes = null);

/// <summary>A download couldn't start or failed; the message says why, in words to show the user.</summary>
public sealed class DownloadException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// Downloads episodes to the download folder, in a folder for each anime named with its AniList and MyAnimeList ids
/// (see <see cref="DownloadNames"/>), so they can be found again by id. A video is downloaded as its stream is: a file
/// as it is, an HLS stream (.m3u8) with yt-dlp, and a torrent through the selected torrent streamer.
/// Downloads run in the background; <see cref="Downloads"/> lists them, and changes as they go.
/// </summary>
public sealed class DownloadService(
	WatchService watch,
	IFactory<ITorrentStreamer, Guid> torrentStreamers,
	IAnimeSettingsStore animeSettings,
	IAnimeRelations relations,
	IPreferences preferences,
	ILogger<DownloadService> logger) : ObservableObject, IDisposable
{
	// What players send, and a stream's own headers replace: Cloudflare turns requests without an Accept away at its edge
	// (AnimePahe's CDN: 403), and some CDNs ones without a browser's User-Agent. Connecting as the player would, so links
	// that only work from the address that asked for them (Re:ANIME's) download, without waiting out broken IPv6.
	private readonly HttpClient _http = new(new SocketsHttpHandler { ConnectCallback = HttpConnect.PreferIPv6Async })
	{
		DefaultRequestHeaders =
		{
			{ "Accept", "*/*" },
			{ "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36" },
		},
	};

	private static readonly TimeSpan s_reportInterval = TimeSpan.FromMilliseconds(500);

	// One download at a time: CDNs turn many at once away (AnimePahe's: 429), and a provider may not take parallel lookups.
	private readonly SemaphoreSlim _queue = new(1, 1);

	/// <summary>How many downloads are going or waiting their turn.</summary>
	public int ActiveCount => Downloads.Count(x => x.IsActive);

	/// <summary>This session's downloads, newest first.</summary>
	public ObservableCollection<DownloadItem> Downloads { get; } = [];

	/// <summary>Downloads episode <paramref name="number"/> from the anime's provider; see <see cref="DownloadEpisodesAsync"/>.</summary>
	public async Task<DownloadItem> DownloadEpisodeAsync(AnimeModel anime, int number) =>
		(await DownloadEpisodesAsync(anime, number, number)).FirstOrDefault()
		?? throw new OperationCanceledException("No show was picked.");

	/// <summary>
	/// Downloads episodes <paramref name="from"/> to <paramref name="to"/> from the anime's provider, as playing them would
	/// find them (the show it's matched with, or one the user picks; none, if they don't). Returns once they're queued; each
	/// item says how it goes, or why it couldn't be downloaded.
	/// </summary>
	public async Task<IReadOnlyList<DownloadItem>> DownloadEpisodesAsync(AnimeModel anime, int from, int to)
	{
		IAnimeProvider provider;
		List<ProviderEpisode> episodes;
		try
		{
			provider = watch.GetProvider(anime) ?? throw new DownloadException("There's no anime provider to download from: install one from the Plugins page.");
			if (await watch.FindOrPickShowAsync(provider, anime, CancellationToken.None) is not { } show)
			{
				return [];
			}

			await relations.EnsureAvailableAsync(CancellationToken.None);
			episodes = await provider.GetEpisodesAsync(show.Id, CancellationToken.None);
		}
		catch (Exception ex)
		{
			// Shown as a failed download, where the user is looking for it.
			var failed = await AddAsync(anime, anime.Title, from);
			End(failed, ex);
			return [failed];
		}

		var numbering = animeSettings.Get(anime).EpisodeNumbering;
		var items = new List<DownloadItem>();
		for (var number = from; number <= to; number++)
		{
			var item = await AddAsync(anime, anime.Title, number);
			items.Add(item);
			var episodeNumber = number;
			// The stream is found when its turn comes: a provider's links may not last while others download.
			_ = RunAsync(item, anime, number, anime.Title, async ct =>
			{
				var episode = watch.PickEpisode(episodes, anime, episodeNumber, numbering)
							  ?? throw new DownloadException($"Episode {episodeNumber} isn't on {provider.Name} yet.");
				return (await provider.GetServersAsync(episode, ct)).FirstOrDefault()
					   ?? throw new DownloadException($"{provider.Name} has no video for episode {episodeNumber}.");
			});
		}

		return items;
	}

	/// <summary>
	/// Downloads a video the user chose on a provider as the anime's episode <paramref name="number"/>; for an anime that
	/// isn't from the database (no ids), into a folder named after <paramref name="title"/> alone.
	/// </summary>
	public async Task<DownloadItem> DownloadAsync(AnimeModel? anime, string title, int number, VideoServer server)
	{
		var item = await AddAsync(anime, title, number);
		_ = RunAsync(item, anime, number, title, _ => Task.FromResult(server));
		return item;
	}

	public void Dispose() => _http.Dispose();

	private async Task<DownloadItem> AddAsync(AnimeModel? anime, string title, int number)
	{
		long? anilist = null, mal = null;
		if (anime is { Id: > 0 })
		{
			anilist = await FindIdAsync(anime, nameof(AnimeId.Anilist));
			mal = await FindIdAsync(anime, nameof(AnimeId.MyAnimeList));
		}

		var item = new DownloadItem
		{
			Title = $"{title} · Episode {number}",
			Folder = Path.Combine(preferences.DownloadFolder, DownloadNames.Folder(title, anilist, mal)),
			Message = "Starting…",
		};
		item.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(DownloadItem.State))
			{
				OnPropertyChanged(nameof(ActiveCount));
			}
		};
		Downloads.Insert(0, item);
		OnPropertyChanged(nameof(ActiveCount));
		return item;
	}

	private async Task<long?> FindIdAsync(AnimeModel anime, string service)
	{
		try
		{
			return await anime.FindIdAsync(service);
		}
		catch (Exception ex)
		{
			// Named without that id, rather than not downloaded.
			logger.LogWarning(ex, "Couldn't find {Service}'s id for {Anime}", service, anime.Title);
			return null;
		}
	}

	/// <summary>
	/// Downloads the episode into the item's folder once the downloads before it are done, from the stream
	/// <paramref name="getServer"/> finds then; the item says how it goes.
	/// </summary>
	private async Task RunAsync(DownloadItem item, AnimeModel? anime, int number, string title, Func<CancellationToken, Task<VideoServer>> getServer)
	{
		var ct = item.Token;
		var name = DownloadNames.Episode(title, number);
		var part = Path.Combine(item.Folder, name + ".part");
		item.Message = "Waiting for the downloads before it…";
		try
		{
			await _queue.WaitAsync(ct);
		}
		catch (OperationCanceledException ex)
		{
			End(item, ex);
			return;
		}

		try
		{
			if (Directory.Exists(item.Folder) &&
				Directory.EnumerateFiles(item.Folder, name + ".*").FirstOrDefault(x => !x.EndsWith(".part", StringComparison.OrdinalIgnoreCase)) is { } existing)
			{
				item.FilePath = existing;
				item.Progress = 1;
				item.Message = "Already downloaded";
				item.State = DownloadState.Completed;
				return;
			}

			item.Message = "Finding the stream…";
			var server = await getServer(ct);
			if (server.Url.IsFile)
			{
				throw new DownloadException("It's already on this PC: its provider plays your downloads.");
			}

			Directory.CreateDirectory(item.Folder);
			await SaveInfoAsync(anime, title, item.Folder, ct);
			// A torrent is downloaded as it'd be played: from the stream the torrent streamer serves.
			await using var stream = server.Kind == VideoServerKind.Torrent ? await StreamTorrentAsync(item, server, ct) : null;
			var url = stream?.Url ?? server.Url;
			var headers = stream?.Headers ?? server.Headers;

			item.Message = "Downloading…";
			// Created here, on the UI thread, so it reports there; the download itself runs off it.
			var progress = new Progress<DownloadProgress>(x =>
			{
				if (item.IsActive)
				{
					item.Progress = x.Fraction;
					item.Message = Describe(x);
				}
			});
			var extension = await Task.Run(() => DownloadAsync(url, headers, stream?.File.Path, part, progress, ct), ct);
			var path = Path.ChangeExtension(part, extension);
			File.Move(part, path, overwrite: true);
			item.FilePath = path;
			item.Progress = 1;
			item.Message = $"Downloaded · {Size(new FileInfo(path).Length)}";
			item.State = DownloadState.Completed;
		}
		catch (Exception ex)
		{
			End(item, ex);
			// Can't be resumed, so of no use: the file so far, and yt-dlp's fragments beside it (episode.part.part-Frag3...).
			try
			{
				foreach (var leftover in Directory.EnumerateFiles(item.Folder, Path.GetFileName(part) + "*"))
				{
					File.Delete(leftover);
				}
			}
			catch (Exception deleteEx) when (deleteEx is IOException or UnauthorizedAccessException)
			{
				logger.LogWarning(deleteEx, "Couldn't delete {File}", part);
			}
		}
		finally
		{
			_queue.Release();
		}
	}

	/// <summary>
	/// Keeps what the Downloads provider shows of the anime in its folder, unless it's there already: its titles, to find
	/// it by any of them, and its cover. The episode downloads without them if they can't be had.
	/// </summary>
	private async Task SaveInfoAsync(AnimeModel? anime, string title, string folder, CancellationToken ct)
	{
		try
		{
			var info = Path.Combine(folder, DownloadNames.InfoFile);
			if (!File.Exists(info))
			{
				string[] others = anime is null
					? []
					: [.. new[] { anime.RomajiTitle, anime.EngTitle }.Concat(anime.AlternateTitles)
										 .Where(x => !string.IsNullOrWhiteSpace(x) && x != title)
										 .Distinct(StringComparer.OrdinalIgnoreCase)];
				await File.WriteAllTextAsync(info, JsonSerializer.Serialize(new DownloadInfo(title, others)), ct);
			}

			if (anime is { Image.Length: > 0 } && !Directory.EnumerateFiles(folder, DownloadNames.CoverFile + ".*").Any())
			{
				var image = new Uri(anime.Image);
				var extension = Path.GetExtension(image.AbsolutePath) is { Length: > 0 } ext ? ext : ".jpg";
				await File.WriteAllBytesAsync(Path.Combine(folder, DownloadNames.CoverFile + extension), await _http.GetByteArrayAsync(image, ct), ct);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Couldn't save {Anime}'s info for its downloads", title);
		}
	}

	/// <summary>
	/// What a download shows as it goes, e.g. "56% · 120.3 MB of 210.5 MB · 6.1 MB/s", leaving out what isn't known.
	/// </summary>
	internal static string Describe(DownloadProgress progress)
	{
		var size = (Size(progress.DownloadedBytes), Size(progress.TotalBytes)) switch
		{
			({ } done, { } total) => $"{done} of {total}",
			({ } done, null) => done,
			_ => null,
		};
		string?[] parts =
		[
			progress.Fraction >= 0 ? progress.Fraction.ToString("P0", CultureInfo.CurrentCulture) : null,
			size,
			Size(progress.BytesPerSecond) is { } speed ? $"{speed}/s" : null,
		];
		return string.Join(" · ", parts.Where(x => x is not null).DefaultIfEmpty("Downloading…"));
	}

	/// <summary>e.g. "1.4 GB", "210.5 MB" or "640 KB"; null when it isn't known.</summary>
	internal static string? Size(double? bytes) => bytes switch
	{
		null or < 0 => null,
		>= 1L << 30 => $"{bytes / (1L << 30):0.0} GB",
		>= 1 << 20 => $"{bytes / (1 << 20):0.0} MB",
		_ => $"{bytes / 1024:0} KB",
	};

	private void End(DownloadItem item, Exception ex)
	{
		(item.State, item.Message) = ex switch
		{
			OperationCanceledException when item.Token.IsCancellationRequested => (DownloadState.Cancelled, "Cancelled"),
			DownloadException or TorrentStreamException or NotSupportedException => (DownloadState.Failed, ex.Message),
			_ => (DownloadState.Failed, $"Couldn't download it: {ex.Message}"),
		};
		if (item.State == DownloadState.Failed)
		{
			logger.LogWarning(ex, "Couldn't download {Title}", item.Title);
		}
	}

	private async Task<TorrentStream> StreamTorrentAsync(DownloadItem item, VideoServer server, CancellationToken ct)
	{
		if (torrentStreamers.CreateDefault() is not { } streamer || !streamer.IsAvailable())
		{
			throw new DownloadException("Downloading a torrent needs a torrent streamer, e.g. rqbit: set one up in Settings.");
		}

		item.Message = $"Starting {streamer.Name}…";
		// shortcut: the stream is read in order, which is slower than a torrent client downloading the whole thing; have rqbit download to the folder itself if that's too slow.
		return await streamer.StreamAsync(server.Url, TorrentFile.LargestVideo, new Progress<string>(x => item.Message = x), ct);
	}

	/// <summary>Writes the video at <paramref name="url"/> to <paramref name="output"/>; returns its extension.</summary>
	private async Task<string> DownloadAsync(Uri url, IReadOnlyDictionary<string, string> headers, string? fileName, string output,
											 IProgress<DownloadProgress> progress, CancellationToken ct)
	{
		// Not fetched here first: AnimePahe's CDN turns .NET's requests away (403).
		if (url.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
		{
			return await DownloadWithYtDlpAsync(url, headers, output, progress, ct);
		}

		using var response = await GetAsync(url, headers, ct);
		var type = response.Content.Headers.ContentType?.MediaType ?? "";
		if (type.Contains("mpegurl", StringComparison.OrdinalIgnoreCase))
		{
			return await DownloadWithYtDlpAsync(url, headers, output, progress, ct);
		}

		// An error page, not the video.
		if (type.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || type.EndsWith("json", StringComparison.OrdinalIgnoreCase))
		{
			throw new DownloadException($"The stream isn't a video file ({type}).");
		}

		var length = response.Content.Headers.ContentLength;
		await using var file = File.Create(output);
		await using var body = await response.Content.ReadAsStreamAsync(ct);
		var buffer = new byte[81920];
		long written = 0, writtenAtReport = 0;
		var sinceReport = Stopwatch.StartNew();
		int read;
		while ((read = await body.ReadAsync(buffer, ct)) > 0)
		{
			await file.WriteAsync(buffer.AsMemory(0, read), ct);
			written += read;
			// Twice a second, not every read: each report is a trip to the UI thread, and the speed is over that time.
			if (sinceReport.Elapsed >= s_reportInterval)
			{
				progress.Report(new DownloadProgress(length > 0 ? (double)written / length.Value : -1,
													 (written - writtenAtReport) / sinceReport.Elapsed.TotalSeconds, written, length));
				writtenAtReport = written;
				sinceReport.Restart();
			}
		}

		return Extension(fileName ?? url.AbsolutePath, type);
	}

	private static string Extension(string path, string type) => type.ToLowerInvariant() switch
	{
		"video/x-matroska" => ".mkv",
		"video/webm" => ".webm",
		"video/mp2t" => ".ts",
		"video/mp4" => ".mp4",
		_ => new TorrentFile(0, path, 0).IsVideo ? Path.GetExtension(path).ToLowerInvariant() : ".mp4",
	};

	/// <summary>
	/// Downloads an HLS stream (its best video and audio) with yt-dlp, into an MPEG-TS file. It connects as Chrome does:
	/// Cloudflare turns away other clients (AnimePahe's CDN answers .NET, ffmpeg and curl with 403), but not browsers.
	/// </summary>
	private async Task<string> DownloadWithYtDlpAsync(Uri url, IReadOnlyDictionary<string, string> headers, string output,
													  IProgress<DownloadProgress> progress, CancellationToken ct)
	{
		var ytDlp = FindYtDlp() ?? throw new DownloadException(
			"Downloading this stream (HLS) needs yt-dlp. Install it (e.g. \"winget install yt-dlp\") or choose yt-dlp.exe in Settings, then download again.");

		var startInfo = new ProcessStartInfo(ytDlp)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		// Its own HLS downloader (8 fragments at a time) rather than ffmpeg, which would be turned away; nothing to fix up
		// afterwards, so it needs no ffmpeg at all. A few fragments at a time, backing off when the CDN says too many
		// (429), and failing rather than skipping one it can't get, which would leave a gap in the episode. % starts a
		// field in yt-dlp's output template.
		string[] args =
		[
			"--no-config", "--no-playlist", "--impersonate", "chrome", "--hls-prefer-native", "--fixup", "never", "--newline",
			"-N", "3", "--fragment-retries", "15", "--retry-sleep", "fragment:exp=2:60", "--abort-on-unavailable-fragments",
			"--progress-template", "download:[totoro] %(progress.fragment_index)s/%(progress.fragment_count)s %(progress.speed)s %(progress.downloaded_bytes)s %(progress.total_bytes_estimate)s",
			"-o", output.Replace("%", "%%", StringComparison.Ordinal),
		];
		foreach (var arg in args)
		{
			startInfo.ArgumentList.Add(arg);
		}

		foreach (var (name, value) in headers)
		{
			startInfo.ArgumentList.Add("--add-header");
			startInfo.ArgumentList.Add($"{name}:{value}");
		}

		startInfo.ArgumentList.Add(url.AbsoluteUri);

		using var process = new Process { StartInfo = startInfo };
		var errors = new List<string>();
		process.ErrorDataReceived += (_, e) =>
		{
			if (e.Data is { } line && line.StartsWith("ERROR:", StringComparison.Ordinal))
			{
				lock (errors)
				{
					errors.Add(line["ERROR:".Length..].Trim());
				}
			}
		};
		process.OutputDataReceived += (_, e) =>
		{
			// "[totoro] 12/145 1234567.8 5000000 60000000.0": fragments downloaded, of how many, bytes a second, bytes
			// downloaded, and the size it expects from the fragments so far; "NA" for what isn't known yet.
			if (e.Data is { } line && line.StartsWith("[totoro] ", StringComparison.Ordinal) &&
				line["[totoro] ".Length..].Split(' ', '/') is [var done, var count, var speed, var downloaded, var total] &&
				int.TryParse(done, out var fragment) && int.TryParse(count, out var fragments) && fragments > 0)
			{
				progress.Report(new DownloadProgress(Math.Clamp((double)fragment / fragments, 0, 1), Number(speed), Number(downloaded), Number(total)));
			}

			static double? Number(string text) => double.TryParse(text, CultureInfo.InvariantCulture, out var number) ? number : null;
		};

		process.Start();
		ChildProcesses.Add(process);
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		try
		{
			await process.WaitForExitAsync(ct);
		}
		catch (OperationCanceledException)
		{
			process.Kill(entireProcessTree: true);
			await process.WaitForExitAsync(CancellationToken.None);
			throw;
		}

		if (process.ExitCode != 0)
		{
			string reason;
			lock (errors)
			{
				reason = errors.LastOrDefault() ?? $"exit code {process.ExitCode}";
			}

			throw new DownloadException($"yt-dlp couldn't download it: {reason}");
		}

		return ".ts";
	}

	/// <summary>The yt-dlp.exe the user chose; if they haven't, the one on the PATH.</summary>
	private string? FindYtDlp()
	{
		if (preferences.YtDlpPath is { Length: > 0 } chosen)
		{
			return File.Exists(chosen) ? chosen : throw new DownloadException($"yt-dlp isn't at {chosen} any more: choose it again on the Downloads page.");
		}

		// The user's and machine's PATH as they are now (from the registry), so yt-dlp installed while Totoro runs is found.
		return new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine }
			   .SelectMany(x => (Environment.GetEnvironmentVariable("PATH", x) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			   .Select(x => Path.Combine(Environment.ExpandEnvironmentVariables(x), "yt-dlp.exe"))
			   .FirstOrDefault(File.Exists);
	}

	/// <summary>Gets <paramref name="url"/> with the headers the stream needs, up to its headers; fails on an error status.</summary>
	private async Task<HttpResponseMessage> GetAsync(Uri url, IReadOnlyDictionary<string, string> headers, CancellationToken ct)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, url);
		foreach (var (name, value) in headers)
		{
			request.Headers.TryAddWithoutValidation(name, value);
		}

		var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
		if (!response.IsSuccessStatusCode)
		{
			response.Dispose();
			throw new DownloadException($"{url.Host} refused it: {(int)response.StatusCode} {response.ReasonPhrase}");
		}

		return response;
	}
}
