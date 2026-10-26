using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Helpers;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Relations;
using Totoro.Core.Services;
using Totoro.Core.Settings;
using Totoro.Helpers;
using Totoro.Modules.MediaPlayers.Mpv;

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

	/// <summary>The anime provider it's from, e.g. "AnimePahe"; empty when that isn't known.</summary>
	public string Provider { get; init; } = "";

	/// <summary>What downloads it: "HTTP", "yt-dlp" or the torrent streamer, e.g. "rqbit"; empty until it starts.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Source))]
	public partial string Downloader { get; internal set; } = "";

	/// <summary>Where it's from and what downloads it, e.g. "AnimePahe · yt-dlp".</summary>
	public string Source => string.Join(" · ", new[] { Provider, Downloader }.Where(x => x.Length > 0));

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

/// <summary>An anime's folder in the download folder (see <see cref="DownloadNames"/>).</summary>
/// <param name="Name">The folder's name, which the Downloads provider knows the show by.</param>
/// <param name="Info">Its info file, or null when it has none (or a broken one).</param>
/// <param name="Episodes">How many episodes are downloaded in it.</param>
/// <param name="Cover">Its cover image's file URI, or empty.</param>
public sealed record DownloadedAnime(string Name, string Path, DownloadInfo? Info, int Episodes, long Bytes, string Cover)
{
	public string Title => Info?.Title ?? Name;

	/// <summary>e.g. "3 episodes · 1.3 GB · AniList 166794 · MyAnimeList 55866", or that it has no info.</summary>
	public string Summary => string.Join(" · ", new[]
	{
		Episodes == 1 ? "1 episode" : $"{Episodes} episodes",
		DownloadService.Size(Bytes),
		Info is null ? "No info: add it so Downloads finds it by its ids" : null,
		Info?.AnilistId is { } anilist ? $"AniList {anilist}" : null,
		Info?.MalId is { } mal ? $"MyAnimeList {mal}" : null,
		Info is not null && Info.AnilistId is null && Info.MalId is null ? "No ids" : null,
		Info?.Segments is { Count: > 0 } segments ? $"Opening and ending of {segments.Count} of {Episodes}" : null,
	}.Where(x => x is not null));

	public void ShowInExplorer() => Process.Start("explorer.exe", $"\"{Path}\"");
}

/// <param name="Fraction">How much is downloaded, from 0 to 1; -1 when the size isn't known.</param>
/// <param name="BytesPerSecond">How fast it's going: over the last second, or for yt-dlp the last few (see <see cref="SpeedMeter"/>); null when that isn't known.</param>
/// <param name="DownloadedBytes">How much is downloaded; null when that isn't known.</param>
/// <param name="TotalBytes">The whole size, or an estimate of it; null when that isn't known.</param>
internal readonly record struct DownloadProgress(double Fraction, double? BytesPerSecond, double? DownloadedBytes = null, double? TotalBytes = null);

/// <summary>
/// A download's speed over the last few seconds, from how much it had downloaded at each <see cref="Next"/>: steady where a
/// second's own speed jumps about with the network's bursts.
/// </summary>
internal sealed class SpeedMeter
{
	private static readonly TimeSpan s_window = TimeSpan.FromSeconds(5);
	private readonly List<(long Time, double Bytes)> _samples = [];

	/// <summary>The speed in bytes a second, now that <paramref name="bytes"/> are downloaded; null until it's known.</summary>
	public double? Next(double? bytes)
	{
		if (bytes is not { } now)
		{
			return null;
		}

		// Fewer than before: it started over, so the earlier samples say nothing about now.
		if (_samples.Count > 0 && now < _samples[^1].Bytes)
		{
			_samples.Clear();
		}

		var time = Stopwatch.GetTimestamp();
		_samples.Add((time, now));
		// Measured from the latest sample a window ago, so a stall longer than the window still shows as slow.
		while (_samples.Count > 2 && Stopwatch.GetElapsedTime(_samples[1].Time, time) >= s_window)
		{
			_samples.RemoveAt(0);
		}

		var (since, then) = _samples[0];
		var seconds = Stopwatch.GetElapsedTime(since, time).TotalSeconds;
		return seconds > 0 ? (now - then) / seconds : null;
	}
}

/// <summary>A download couldn't start or failed; the message says why, in words to show the user.</summary>
public sealed class DownloadException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// Downloads episodes to the download folder, in a folder for each anime with its AniList and MyAnimeList ids in its info
/// file (see <see cref="DownloadNames"/>). A video is downloaded as its stream is: a file
/// as it is, an HLS stream (.m3u8) with yt-dlp, and a torrent through the selected torrent streamer.
/// Downloads run in the background; <see cref="Downloads"/> lists them, and changes as they go.
/// </summary>
public sealed class DownloadService(
	WatchService watch,
	IFactory<ITorrentStreamer, Guid> torrentStreamers,
	IAnimeSettingsStore animeSettings,
	IAnimeRelations relations,
	IPreferences preferences,
	AnimeSongService songs,
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

	private static readonly TimeSpan s_reportInterval = TimeSpan.FromSeconds(1);

	// One download at a time: CDNs turn many at once away (AnimePahe's: 429), and a provider may not take parallel lookups.
	private readonly SemaphoreSlim _queue = new(1, 1);

	// Finding openings and endings, one episode at a time, so info files are written one at a time.
	private readonly SemaphoreSlim _detecting = new(1, 1);

	// Songs' fingerprints and lengths, by their audio's URL: a season's episodes share them.
	private readonly ConcurrentDictionary<Uri, (uint[] Print, TimeSpan Length)> _songPrints = new();

	/// <summary>How many downloads are going or waiting their turn.</summary>
	public int ActiveCount => Downloads.Count(x => x.IsActive);

	/// <summary>This session's downloads, in the order they download.</summary>
	public ObservableCollection<DownloadItem> Downloads { get; } = [];

	/// <summary>Downloads episode <paramref name="number"/> from the anime's provider; see <see cref="DownloadEpisodesAsync"/>.</summary>
	public async Task<DownloadItem> DownloadEpisodeAsync(AnimeModel anime, int number) =>
		(await DownloadEpisodesAsync(anime, number, number)).FirstOrDefault()
		?? throw new OperationCanceledException("No show was picked.");

	/// <summary>
	/// Downloads episodes <paramref name="from"/> to <paramref name="to"/> from <paramref name="provider"/> (by default the
	/// anime's), as playing them would find them (the show it's matched with, or one the user picks; none, if they don't).
	/// Returns once they're queued; each item says how it goes, or why it couldn't be downloaded.
	/// </summary>
	public async Task<IReadOnlyList<DownloadItem>> DownloadEpisodesAsync(AnimeModel anime, int from, int to, IAnimeProvider? provider = null)
	{
		List<ProviderEpisode> episodes;
		try
		{
			provider ??= watch.GetProvider(anime) ?? throw new DownloadException("There's no anime provider to download from: install one from the Plugins page.");
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
			var failed = Add(anime.Title, from, provider?.Name ?? "");
			End(failed, ex);
			return [failed];
		}

		var numbering = animeSettings.Get(anime).EpisodeNumbering;
		var items = new List<DownloadItem>();
		for (var number = from; number <= to; number++)
		{
			var item = Add(anime.Title, number, provider.Name);
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
	/// isn't from the database, without ids. <paramref name="provider"/> is the provider's name, to show.
	/// </summary>
	public DownloadItem Download(AnimeModel? anime, string title, int number, VideoServer server, string provider)
	{
		var item = Add(title, number, provider);
		_ = RunAsync(item, anime, number, title, _ => Task.FromResult(server));
		return item;
	}

	/// <summary>Whether the anime's episode <paramref name="number"/> is in the download folder (not part-downloaded).</summary>
	public bool IsDownloaded(string title, int number) =>
		FindDownloaded(Path.Combine(preferences.DownloadFolder, DownloadNames.Folder(title)), DownloadNames.Episode(title, number)) is not null;

	/// <summary>The episode file called <paramref name="name"/> (any extension) in <paramref name="folder"/>; null if there's none.</summary>
	private static string? FindDownloaded(string folder, string name) =>
		Directory.Exists(folder)
			// Not its subtitles (name.English.en.vtt), which the pattern matches too.
			? Directory.EnumerateFiles(folder, name + ".*").FirstOrDefault(x => Path.GetFileNameWithoutExtension(x) == name && !x.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
			: null;

	/// <summary>Shows the download folder in File Explorer, making it first if nothing's been downloaded yet.</summary>
	public void OpenFolder()
	{
		Directory.CreateDirectory(preferences.DownloadFolder);
		Process.Start("explorer.exe", $"\"{preferences.DownloadFolder}\"");
	}

	/// <summary>The anime in the download folder, by title, as <see cref="RefreshLibrary"/> last read them.</summary>
	public ObservableCollection<DownloadedAnime> Library { get; } = [];

	/// <summary>Reads the anime in the download folder into <see cref="Library"/> again.</summary>
	public void RefreshLibrary()
	{
		var root = new DirectoryInfo(preferences.DownloadFolder);
		Library.Clear();
		foreach (var anime in (root.Exists ? root.EnumerateDirectories() : []).Select(ReadAnime).OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase))
		{
			Library.Add(anime);
		}
	}

	/// <summary>Writes the anime's info file, which the Downloads provider finds it by, replacing any it had.</summary>
	public void SaveInfo(string name, DownloadInfo info)
	{
		if (string.IsNullOrWhiteSpace(info.Title))
		{
			throw new DownloadException("It needs a title.");
		}

		string[] others = [.. info.OtherTitles.Select(x => x.Trim()).Where(x => x.Length > 0 && x != info.Title.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)];
		var folder = FindFolder(name).FullName;
		DownloadNames.WriteInfo(folder, info with
		{
			Title = info.Title.Trim(),
			OtherTitles = others,
			AnilistId = info.AnilistId is > 0 ? info.AnilistId : null,
			MalId = info.MalId is > 0 ? info.MalId : null,
			// Editing the titles and ids keeps the openings and endings found.
			Segments = info.Segments ?? ReadSegments(folder),
		});
		RefreshLibrary();
	}

	/// <summary>Moves the anime's folder, its episodes and all, to the Recycle Bin.</summary>
	public void Delete(string name)
	{
		var folder = FindFolder(name);
		if (Downloads.Any(x => x.IsActive && string.Equals(Path.GetFullPath(x.Folder), folder.FullName, StringComparison.OrdinalIgnoreCase)))
		{
			throw new DownloadException($"An episode of {name} is downloading: cancel it first.");
		}

		Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(folder.FullName, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
			Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
		RefreshLibrary();
	}

	/// <summary>The anime's folder, found among the download folder's own so a name can't lead out of it.</summary>
	private DirectoryInfo FindFolder(string name)
	{
		var root = new DirectoryInfo(preferences.DownloadFolder);
		return (root.Exists ? root.EnumerateDirectories() : []).FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			   ?? throw new DownloadException($"There's no folder called {name} in the download folder.");
	}

	private DownloadedAnime ReadAnime(DirectoryInfo folder)
	{
		DownloadInfo? info = null;
		try
		{
			info = DownloadNames.ReadInfo(folder.FullName);
		}
		catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Couldn't read {Folder}'s info", folder.FullName);
		}

		var episodes = folder.EnumerateFiles()
							 .Where(x => !x.Extension.Equals(".part", StringComparison.OrdinalIgnoreCase) && DownloadNames.ParseEpisode(x.Name) is not null)
							 .ToList();
		var cover = folder.EnumerateFiles(DownloadNames.CoverFile + ".*").FirstOrDefault();
		return new DownloadedAnime(folder.Name, folder.FullName, info, episodes.Count, episodes.Sum(x => x.Length),
								   cover is null ? "" : new Uri(cover.FullName).AbsoluteUri);
	}

	/// <summary>
	/// Cancels every download, and returns once the one downloading has deleted what it had so far, or after
	/// <paramref name="timeout"/> if that takes longer. For when the app closes: the queue stays taken, so nothing downloads after.
	/// </summary>
	public async Task CancelAllAsync(TimeSpan timeout)
	{
		foreach (var item in Downloads.Where(x => x.IsActive))
		{
			item.Cancel();
		}

		// Released by the download that had its turn once it's cleaned up; the ones waiting gave up their turn when cancelled.
		await _queue.WaitAsync(timeout);
	}

	public void Dispose() => _http.Dispose();

	private DownloadItem Add(string title, int number, string provider)
	{
		var item = new DownloadItem
		{
			Title = $"{title} · Episode {number}",
			Folder = Path.Combine(preferences.DownloadFolder, DownloadNames.Folder(title)),
			Provider = provider,
			Message = "Starting…",
		};
		item.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(DownloadItem.State))
			{
				OnPropertyChanged(nameof(ActiveCount));
			}
		};
		// In the order they download: the queue's first at the top.
		Downloads.Add(item);
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
			// Kept without that id, rather than not downloaded.
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
			if (FindDownloaded(item.Folder, name) is { } existing)
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
			void Show(DownloadProgress x)
			{
				if (item.IsActive)
				{
					item.Progress = x.Fraction;
					item.Message = Describe(x);
				}
			}

			var streamer = server.Kind == VideoServerKind.Torrent ? GetTorrentStreamer(item) : null;
			string path;
			if (streamer is not null && await DownloadTorrentAsync(streamer, item, server, part, Show, ct) is { } downloaded)
			{
				path = downloaded;
			}
			else
			{
				// A torrent its streamer can't download itself is downloaded as it'd be played: from the stream it serves.
				await using var stream = streamer is null ? null : await StreamTorrentAsync(streamer, item, server, ct);
				var url = stream?.Url ?? server.Url;
				var headers = stream?.Headers ?? server.Headers;

				item.Message = "Downloading…";
				// Created here, on the UI thread, so it reports there; the download itself runs off it.
				var progress = new Progress<DownloadProgress>(Show);
				// A torrent's downloader is its streamer, already shown, not the HTTP it's read with.
				var downloader = new Progress<string>(x =>
				{
					if (stream is null)
					{
						item.Downloader = x;
					}
				});
				var extension = await Task.Run(() => DownloadAsync(url, headers, stream?.File.Path, part, progress, downloader, ct), ct);
				path = Path.ChangeExtension(part, extension);
				File.Move(part, path, overwrite: true);
			}

			await SaveSubtitlesAsync(server, name, item.Folder, ct);

			item.FilePath = path;
			item.Progress = 1;
			item.Message = $"Downloaded · {Size(new FileInfo(path).Length)}";
			item.State = DownloadState.Completed;
			RefreshLibrary();
			// After it's done, so the next download doesn't wait for it.
			_ = DetectSegmentsAsync(number, path, item.Folder);
		}
		catch (Exception ex)
		{
			End(item, ex);
			// Can't be resumed, so of no use: the file so far, yt-dlp's fragments beside it (episode.part.part-Frag3...), and
			// the torrent streamer's folder.
			try
			{
				foreach (var leftover in Directory.EnumerateFiles(item.Folder, Path.GetFileName(part) + "*"))
				{
					File.Delete(leftover);
				}

				if (Directory.Exists(TorrentFolder(part)))
				{
					Directory.Delete(TorrentFolder(part), recursive: true);
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
	/// Saves the stream's subtitle files beside the episode <paramref name="name"/>, for the Downloads provider to play it
	/// with. The episode is kept without any that can't be had.
	/// </summary>
	private async Task SaveSubtitlesAsync(VideoServer server, string name, string folder, CancellationToken ct)
	{
		foreach (var (subtitle, i) in server.Subtitles.Select((x, i) => (x, i)))
		{
			try
			{
				var extension = Path.GetExtension(subtitle.Url.AbsolutePath).ToLowerInvariant();
				extension = extension is ".srt" or ".ass" or ".ssa" ? extension : ".vtt";
				using var response = await GetAsync(subtitle.Url, server.Headers, ct);
				await File.WriteAllBytesAsync(Path.Combine(folder, DownloadNames.Subtitle(name, i + 1, subtitle) + extension), await response.Content.ReadAsByteArrayAsync(ct), ct);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				logger.LogWarning(ex, "Couldn't save the subtitles {Subtitle} of {Episode}", subtitle.Name, name);
			}
		}
	}

	/// <summary>
	/// Keeps what the Downloads provider shows of the anime in its folder, unless it's there already: its titles, to find
	/// it by any of them, its AniList and MyAnimeList ids, and its cover. The episode downloads without them if they can't
	/// be had.
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
				long? anilist = null, mal = null;
				if (anime is { Id: > 0 })
				{
					anilist = await FindIdAsync(anime, nameof(AnimeId.Anilist));
					mal = await FindIdAsync(anime, nameof(AnimeId.MyAnimeList));
				}

				await File.WriteAllTextAsync(info, JsonSerializer.Serialize(new DownloadInfo(title, others, anilist, mal)), ct);
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
	/// Finds the openings and endings of the anime's downloaded episodes that don't have them in its info file: ones
	/// downloaded before they were looked for, put in its folder by hand, or whose songs weren't found then. Returns how
	/// many episodes were looked at, and how many of them they were found for.
	/// </summary>
	public async Task<(int Checked, int Found)> FindMissingSegmentsAsync(string name)
	{
		if (!File.Exists(FfmpegPath))
		{
			throw new DownloadException("It needs FFmpeg, which comes with mpv: set up the MPV media player first.");
		}

		var folder = FindFolder(name);
		var info = DownloadNames.ReadInfo(folder.FullName);
		if (info?.MalId is null)
		{
			throw new DownloadException($"{name}'s songs are found by its MyAnimeList id: add it with Edit info.");
		}

		var missing = folder.EnumerateFiles()
							.Where(x => !x.Extension.Equals(".part", StringComparison.OrdinalIgnoreCase))
							.Select(x => (File: x, Number: DownloadNames.ParseEpisode(x.Name)))
							.Where(x => x.Number is { } number && info.Segments?.ContainsKey(number) != true)
							.ToList();
		var found = 0;
		foreach (var (file, number) in missing)
		{
			found += await DetectSegmentsAsync(number!.Value, file.FullName, folder.FullName) ? 1 : 0;
		}

		RefreshLibrary();
		return (missing.Count, found);
	}

	private static string FfmpegPath => Path.Combine(MpvInstaller.Folder, "ffmpeg.exe");

	/// <summary>
	/// Finds the episode's opening and ending by the sound of the anime's songs on AnimeThemes (see
	/// <see cref="AudioFingerprint"/>), and keeps them in the anime's info file, for the Downloads provider to play it with.
	/// Needs mpv's ffmpeg and the anime's MyAnimeList id in its info file; without them, or if the songs aren't found, the
	/// episode has none (and Aniskip's are used). Returns whether any were found.
	/// </summary>
	private async Task<bool> DetectSegmentsAsync(int number, string file, string folder)
	{
		var ffmpeg = FfmpegPath;
		if (!File.Exists(ffmpeg))
		{
			return false;
		}

		await _detecting.WaitAsync();
		var title = Path.GetFileName(folder);
		try
		{
			if (DownloadNames.ReadInfo(folder)?.MalId is not { } malId)
			{
				return false;
			}

			var candidates = (await songs.GetSongsAsync(malId))
				.Where(x => x.Kind is AnimeSongKind.Opening or AnimeSongKind.Ending && x.Audio is not null && AudioFingerprint.Covers(x.Episodes, number))
				.ToList();
			if (candidates.Count == 0)
			{
				return false;
			}

			var episode = await Task.Run(async () => AudioFingerprint.Compute(await AudioFingerprint.DecodeAsync(ffmpeg, file, CancellationToken.None)));
			List<MediaSegment> segments = [];
			foreach (var kind in candidates.GroupBy(x => x.Kind))
			{
				// Of a kind's songs (when the episodes they're in aren't known), the one that matches best.
				(MediaSegment Segment, double ErrorRate)? best = null;
				foreach (var song in kind)
				{
					var (print, length) = await GetSongPrintAsync(ffmpeg, song.Audio!);
					if (await Task.Run(() => AudioFingerprint.Find(print, episode)) is { } found && (best is null || found.ErrorRate < best.Value.ErrorRate))
					{
						var type = kind.Key == AnimeSongKind.Opening ? MediaSegmentType.Opening : MediaSegmentType.Ending;
						best = (new MediaSegment(type, found.Start, found.Start + length), found.ErrorRate);
					}
				}

				if (best is { } match)
				{
					segments.Add(match.Segment);
				}
			}

			logger.LogInformation("Found {Count} of {Anime} episode {Number}'s opening and ending", segments.Count, title, number);
			// Read again: the info may have been edited while the songs were looked for.
			if (segments.Count == 0 || DownloadNames.ReadInfo(folder) is not { } info)
			{
				return false;
			}

			var all = new Dictionary<int, IReadOnlyList<MediaSegment>>(info.Segments ?? new Dictionary<int, IReadOnlyList<MediaSegment>>())
			{
				[number] = [.. segments.OrderBy(x => x.Start)],
			};
			DownloadNames.WriteInfo(folder, info with { Segments = all });
			return true;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't find {Anime} episode {Number}'s opening and ending", title, number);
			return false;
		}
		finally
		{
			_detecting.Release();
		}
	}

	private async Task<(uint[] Print, TimeSpan Length)> GetSongPrintAsync(string ffmpeg, Uri audio)
	{
		if (_songPrints.TryGetValue(audio, out var known))
		{
			return known;
		}

		// Kept across restarts: AnimeThemes' files don't change. The folder's version changes with how prints are taken.
		var saved = FileHelper.GetPath(Path.Combine("Fingerprints", "v1",
			Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(audio.AbsoluteUri)))[..32] + ".fp"));
		if (AudioFingerprint.Load(saved) is { } loaded)
		{
			return _songPrints[audio] = loaded;
		}

		var file = Path.GetTempFileName();
		try
		{
			using (var request = new HttpRequestMessage(HttpMethod.Get, audio))
			{
				// AnimeThemes refuses requests without one of its own.
				request.Headers.UserAgent.ParseAdd("Totoro/1.0 (anime tracker)");
				using var response = await _http.SendAsync(request);
				await File.WriteAllBytesAsync(file, await response.EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync());
			}

			var samples = await AudioFingerprint.DecodeAsync(ffmpeg, file, CancellationToken.None);
			var (print, length) = await Task.Run(() => (AudioFingerprint.Compute(samples), AudioFingerprint.Duration(samples)));
			AudioFingerprint.Save(saved, print, length);
			return _songPrints[audio] = (print, length);
		}
		finally
		{
			File.Delete(file);
		}
	}

	/// <summary>The openings and endings found for the folder's episodes, from its info file; null if none (or it can't be read).</summary>
	private IReadOnlyDictionary<int, IReadOnlyList<MediaSegment>>? ReadSegments(string folder)
	{
		try
		{
			return DownloadNames.ReadInfo(folder)?.Segments;
		}
		catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
		{
			logger.LogWarning(ex, "Couldn't read {Folder}'s info", folder);
			return null;
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

	/// <summary>The torrent streamer to download a torrent with, which the item then says it's downloaded by.</summary>
	private ITorrentStreamer GetTorrentStreamer(DownloadItem item)
	{
		if (torrentStreamers.CreateDefault() is not { } streamer || !streamer.IsAvailable())
		{
			throw new DownloadException("Downloading a torrent needs a torrent streamer, e.g. rqbit: set one up in Settings.");
		}

		item.Downloader = streamer.Name;
		return streamer;
	}

	/// <summary>Where the torrent streamer downloads the episode <paramref name="part"/> is for, before it's renamed.</summary>
	private static string TorrentFolder(string part) => part + "-torrent";

	/// <summary>
	/// Has the torrent streamer download the episode itself (rqbit: as a torrent client does, faster than reading its
	/// stream), into <see cref="TorrentFolder"/>, then names it as the episode; null if the streamer can't.
	/// </summary>
	private static async Task<string?> DownloadTorrentAsync(ITorrentStreamer streamer, DownloadItem item, VideoServer server, string part,
															Action<DownloadProgress> show, CancellationToken ct)
	{
		item.Message = $"Starting {streamer.Name}…";
		var folder = TorrentFolder(part);
		string file;
		try
		{
			// The speed is the streamer's own, as it reports it.
			file = await streamer.DownloadAsync(server.Url, TorrentFile.LargestVideo, folder, new Progress<string>(x => item.Message = x),
				new Progress<TorrentDownloadProgress>(x => show(new DownloadProgress(x.TotalBytes > 0 ? (double)x.DownloadedBytes / x.TotalBytes : -1,
																					 x.BytesPerSecond, x.DownloadedBytes, x.TotalBytes))), ct);
		}
		catch (NotSupportedException)
		{
			return null;
		}

		var path = Path.ChangeExtension(part, Extension(file, ""));
		for (var attempt = 1; ; attempt++)
		{
			try
			{
				File.Move(file, path, overwrite: true);
				break;
			}
			catch (IOException) when (attempt < 10)
			{
				// The streamer lets go of the file shortly after it's done with the torrent.
				await Task.Delay(500, ct);
			}
		}

		try
		{
			Directory.Delete(folder, recursive: true);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// An empty folder left behind isn't worth failing the download for: the episode itself is there.
		}

		return path;
	}

	private static async Task<TorrentStream> StreamTorrentAsync(ITorrentStreamer streamer, DownloadItem item, VideoServer server, CancellationToken ct)
	{
		item.Message = $"Starting {streamer.Name}…";
		return await streamer.StreamAsync(server.Url, TorrentFile.LargestVideo, new Progress<string>(x => item.Message = x), ct);
	}

	/// <summary>Writes the video at <paramref name="url"/> to <paramref name="output"/>; returns its extension.</summary>
	private async Task<string> DownloadAsync(Uri url, IReadOnlyDictionary<string, string> headers, string? fileName, string output,
											 IProgress<DownloadProgress> progress, IProgress<string> downloader, CancellationToken ct)
	{
		// Not fetched here first: AnimePahe's CDN turns .NET's requests away (403).
		if (url.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
		{
			downloader.Report("yt-dlp");
			return await DownloadWithYtDlpAsync(url, headers, output, progress, ct);
		}

		using var response = await GetAsync(url, headers, ct);
		var type = response.Content.Headers.ContentType?.MediaType ?? "";
		if (type.Contains("mpegurl", StringComparison.OrdinalIgnoreCase))
		{
			downloader.Report("yt-dlp");
			return await DownloadWithYtDlpAsync(url, headers, output, progress, ct);
		}

		downloader.Report("HTTP");

		// An error page, not the video.
		if (type.StartsWith("text/", StringComparison.OrdinalIgnoreCase) || type.EndsWith("json", StringComparison.OrdinalIgnoreCase))
		{
			throw new DownloadException($"The stream isn't a video file ({type}).");
		}

		var length = response.Content.Headers.ContentLength;
		await using var file = File.Create(output);
		await using var body = await response.Content.ReadAsStreamAsync(ct);
		var buffer = new byte[81920];
		long written = 0, reported = 0;
		var sinceReport = Stopwatch.StartNew();
		int read;
		while ((read = await body.ReadAsync(buffer, ct)) > 0)
		{
			await file.WriteAsync(buffer.AsMemory(0, read), ct);
			written += read;
			// Once a second, not every read: each report is a trip to the UI thread. The speed is that second's, as received:
			// unlike yt-dlp's fragments, a stream arrives steadily enough not to need smoothing.
			if (sinceReport.Elapsed >= s_reportInterval)
			{
				var speed = (written - reported) / sinceReport.Elapsed.TotalSeconds;
				progress.Report(new DownloadProgress(length > 0 ? (double)written / length.Value : -1, speed, written, length));
				reported = written;
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
	/// Downloads an HLS stream (its best video and audio) with yt-dlp, into an MPEG-TS file, or one ffmpeg merged when its
	/// audio is apart from its video; returns the file's extension. It connects as Chrome does:
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
			"--progress-template", "download:[totoro] %(progress.fragment_index)s/%(progress.fragment_count)s %(progress.downloaded_bytes)s %(progress.total_bytes_estimate)s",
			"-o", output.Replace("%", "%%", StringComparison.Ordinal),
		];
		foreach (var arg in args)
		{
			startInfo.ArgumentList.Add(arg);
		}

		// To merge a stream whose audio is apart from its video (KickAssAnime's): mpv's download comes with ffmpeg, else
		// yt-dlp looks for one on the PATH.
		if (File.Exists(Path.Combine(Modules.MediaPlayers.Mpv.MpvInstaller.Folder, "ffmpeg.exe")))
		{
			startInfo.ArgumentList.Add("--ffmpeg-location");
			startInfo.ArgumentList.Add(Modules.MediaPlayers.Mpv.MpvInstaller.Folder);
		}

		foreach (var (name, value) in headers)
		{
			startInfo.ArgumentList.Add("--add-header");
			startInfo.ArgumentList.Add($"{name}:{value}");
		}

		startInfo.ArgumentList.Add(url.AbsoluteUri);

		using var process = new Process { StartInfo = startInfo };
		var errors = new List<string>();
		// 0 is long ago, so the first line is reported.
		long lastReport = 0;
		var speedMeter = new SpeedMeter();
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
			// "[totoro] 12/145 5000000 60000000.0": fragments downloaded, of how many, bytes downloaded, and the size it
			// expects from the fragments so far; "NA" for what isn't known yet. Not its speed: that's the fragment it last
			// reported on's, one of several at once. It prints many a second; like a download's, one a second is reported.
			if (Stopwatch.GetElapsedTime(lastReport) >= s_reportInterval &&
				e.Data is { } line && line.StartsWith("[totoro] ", StringComparison.Ordinal) &&
				line["[totoro] ".Length..].Split(' ', '/') is [var done, var count, var downloaded, var total] &&
				int.TryParse(done, out var fragment) && int.TryParse(count, out var fragments) && fragments > 0)
			{
				lastReport = Stopwatch.GetTimestamp();
				var bytes = Number(downloaded);
				progress.Report(new DownloadProgress(Math.Clamp((double)fragment / fragments, 0, 1), speedMeter.Next(bytes), bytes, Number(total)));
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

		if (File.Exists(output))
		{
			return ".ts";
		}

		// Merged from separate video and audio, it's the output with the merged format's extension added (episode.part.mp4).
		foreach (var extension in new[] { ".mp4", ".mkv", ".webm" })
		{
			if (File.Exists(output + extension))
			{
				File.Move(output + extension, output);
				return extension;
			}
		}

		// Without ffmpeg, yt-dlp leaves the video and audio apart (episode.part.f3102.mp4...) and succeeds.
		throw new DownloadException("This stream's audio is apart from its video, which takes ffmpeg to put together: download mpv in its settings (it comes with ffmpeg), or install ffmpeg, then download again.");
	}

	/// <summary>The yt-dlp.exe the user chose; if they haven't, the one on the PATH, or the one downloaded with mpv.</summary>
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
			   .Append(Path.Combine(Modules.MediaPlayers.Mpv.MpvInstaller.Folder, "yt-dlp.exe"))
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
