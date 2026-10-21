using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Totoro.Core.Agent;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;
using Totoro.Playback;

namespace Totoro.Agent;

[Description("How playing an episode went.")]
public sealed record PlayResult(
	[property: Description("Whether it's playing (or still getting ready to).")]
	bool Started,
	[property: Description("What happened, e.g. \"Playing in MPV\", or why it can't play.")]
	string Message,
	[property: Description("When the anime isn't matched with a show on its provider yet: the shows it may be, best first. Pick one with set_provider_show, then play again.")]
	IReadOnlyList<ShowCandidate>? ChooseShow = null);

[Description("A show on an anime provider (where episodes are streamed from).")]
public sealed record ShowCandidate(
	string Id,
	string Title,
	[property: Description("How well its title matched the anime's, from 0 to 1; 1 is an exact match.")]
	double Score);

[Description("An audio or subtitle track of the episode playing.")]
public sealed record PlaybackTrack(
	MediaTrackKind Kind,
	[property: Description("What select_playback_track takes; -1 is \"no subtitles\".")]
	int Id,
	string Name,
	bool IsSelected);

[Description("The shows on the anime's provider it may be, and the one it's matched with, if any.")]
public sealed record ProviderShows(string Provider, ShowCandidate? Matched, IReadOnlyList<ShowCandidate> Candidates);

[Description("An episode of a show on an anime provider.")]
public sealed record ProviderEpisodeItem(
	string Id,
	[property: Description("Its number as the provider numbers it (with absolute numbering, it carries on from a previous season).")]
	float Number);

[Description("An episode downloaded or being downloaded.")]
public sealed record DownloadInfo(
	[property: Description("What cancel_download takes.")]
	string Title,
	DownloadState State,
	[property: Description("How much is downloaded, out of 100, when known.")]
	int? Percent,
	[property: Description("What it's doing, or why it failed.")]
	string Message,
	[property: Description("The provider it's from and what downloads it (HTTP, yt-dlp or the torrent streamer), e.g. \"AnimePahe · yt-dlp\".")]
	string Source,
	[property: Description("The episode's file once downloaded; until then the anime's folder.")]
	string Path);

[Description("Where episodes are downloaded to (a folder for each anime, named with its title), and the downloads.")]
public sealed record Downloads(string Folder, IReadOnlyList<DownloadInfo> Items);

[Description("An anime's folder in the download folder.")]
public sealed record DownloadedAnimeInfo(
	[property: Description("The folder's name: what edit_downloaded_anime and delete_downloaded_anime take.")]
	string Folder,
	[property: Description("Its title, as its info says; null when it has no info.")]
	string? Title,
	IReadOnlyList<string> OtherTitles,
	long? AnilistId,
	long? MalId,
	[property: Description("How many episodes are downloaded.")]
	int Episodes,
	[property: Description("How much space they take, e.g. \"1.3 GB\".")]
	string Size);

/// <summary>Agent tools for watching and downloading anime (see <see cref="WatchService"/> and <see cref="DownloadService"/>).</summary>
public sealed class PlaybackTools(AnimeLookup lookup, WatchService watch, DownloadService downloads, IPreferences preferences,
								  IFactory<IAnimeProvider, Guid> providers)
{
	// Long enough for a torrent to find peers.
	private static readonly TimeSpan s_startTimeout = TimeSpan.FromSeconds(90);

	// Enums by name, as get_module_settings shows them.
	private static readonly JsonSerializerOptions s_settingOptions = new() { Converters = { new JsonStringEnumConverter() } };

	[AgentTool("play_episode")]
	[Description("Plays an episode of an anime in the media player, resuming where the user left off. Playback continues on its own; " +
				 "it's marked watched when they finish it.")]
	public async Task<PlayResult> PlayEpisodeAsync(
		long animeId,
		[Description("The episode's number; leave it out for the next one the user hasn't watched (past fillers, if they skip them).")] int? episode = null)
	{
		var anime = await lookup.GetAsync(animeId);
		var number = episode ?? await watch.NextToWatchAsync(anime);
		if (watch.GetProvider(anime) is not { } provider)
		{
			throw new AgentToolException("There's no anime provider to watch from.");
		}

		// The UI asks the user to pick the show when it's unclear; the agent picks instead (or asks them).
		var (show, candidates) = await watch.FindShowAsync(provider, anime, CancellationToken.None);
		if (show is null)
		{
			return new PlayResult(false, $"It isn't clear which {provider.Name} show {anime.Title} is.", [.. candidates.Select(ToCandidate)]);
		}

		var status = await watch.StartWatchingAsync(anime, number, s_startTimeout);
		var started = status.IsActive && status.Kind is PlaybackStatusKind.Playing or PlaybackStatusKind.Progress;
		return new PlayResult(started, status.IsActive ? status.Message : "It was cancelled.");
	}

	[AgentTool("get_playback_status")]
	[Description("What's playing, or being got ready to play, and how it's going.")]
	public PlaybackStatus GetPlaybackStatus() => watch.Status;

	[AgentTool("set_playback_paused")]
	[Description("Pauses or resumes the episode playing. Only in the built-in player.")]
	public async Task<string> SetPlaybackPausedAsync(bool paused)
	{
		await Control(() => watch.SetPausedAsync(paused));
		return paused ? "Paused." : "Playing.";
	}

	[AgentTool("seek_playback")]
	[Description("Jumps to a time in the episode playing.")]
	public async Task<string> SeekPlaybackAsync([Description("Where to jump to, in seconds from the start.")] double seconds)
	{
		var position = TimeSpan.FromSeconds(Math.Max(0, seconds));
		await Control(() => watch.SeekAsync(position));
		return $"Jumped to {position.ToString(@"h\:mm\:ss")}.";
	}

	[AgentTool("get_playback_tracks")]
	[Description("The audio and subtitle tracks of the episode playing, and which are selected. Only in the built-in player.")]
	public async Task<IReadOnlyList<PlaybackTrack>> GetPlaybackTracksAsync()
	{
		IReadOnlyList<MediaTrack> tracks = [];
		await Control(() =>
		{
			tracks = watch.GetTracks();
			return Task.CompletedTask;
		});
		return [.. tracks.Select(x => new PlaybackTrack(x.Kind, x.Id, x.Name, x.IsSelected))];
	}

	[AgentTool("select_playback_track")]
	[Description("Switches the audio or subtitles of the episode playing to a track from get_playback_tracks. Returns the tracks as they are now.")]
	public async Task<IReadOnlyList<PlaybackTrack>> SelectPlaybackTrackAsync(
		MediaTrackKind kind,
		[Description("The track's id; -1 turns subtitles off.")] int id)
	{
		await Control(() => watch.SelectTrackAsync(kind, id));
		return await GetPlaybackTracksAsync();
	}

	/// <summary>Controls playback, with the reason it can't (nothing playing, another player) for the agent.</summary>
	private static async Task Control(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (InvalidOperationException ex)
		{
			throw new AgentToolException(ex.Message);
		}
	}

	[AgentTool("find_provider_shows")]
	[Description("The shows on the anime's provider (where its episodes are streamed from) that may be the anime, and the one it's matched with.")]
	public async Task<ProviderShows> FindProviderShowsAsync(
		long animeId,
		[Description("A title to search the provider for instead of the anime's own titles.")] string? query = null,
		[Description("Another provider to look on than the anime's, e.g. to download from it.")] string? provider = null)
	{
		var anime = await lookup.GetAsync(animeId);
		var source = GetProvider(anime, provider);
		var candidates = string.IsNullOrWhiteSpace(query)
			? await WatchService.SearchShowsAsync(source, anime, CancellationToken.None)
			: await source.SearchAsync(query, CancellationToken.None);
		var (matched, _) = await watch.FindShowAsync(source, anime, CancellationToken.None);
		return new ProviderShows(source.Name, matched is null ? null : ToCandidate(matched), [.. candidates.Select(ToCandidate)]);
	}

	[AgentTool("set_provider_show")]
	[Description("Matches an anime with a show on its provider (from find_provider_shows), so its episodes are played (or downloaded) from that show.")]
	public async Task<string> SetProviderShowAsync(
		long animeId,
		string showId,
		string showTitle,
		[Description("The provider the show is on, when it isn't the anime's.")] string? provider = null)
	{
		var anime = await lookup.GetAsync(animeId);
		var source = GetProvider(anime, provider);
		watch.SetShow(anime, source, new ProviderShow(showId, showTitle, 1));
		return $"{anime.Title} is \"{showTitle}\" on {source.Name}.";
	}

	[AgentTool("search_provider")]
	[Description("Searches an anime provider for shows, to watch an episode from it with play_from_provider without changing the anime's settings.")]
	public async Task<IReadOnlyList<ShowCandidate>> SearchProviderAsync(
		[Description("The provider's name (get_anime_settings lists them).")] string provider,
		string query)
	{
		return [.. (await GetProvider(provider).SearchAsync(query, CancellationToken.None)).Select(ToCandidate)];
	}

	[AgentTool("get_provider_episodes")]
	[Description("The episodes a show on an anime provider (from search_provider) has, by the provider's numbering.")]
	public async Task<IReadOnlyList<ProviderEpisodeItem>> GetProviderEpisodesAsync(string provider, string showId)
	{
		var episodes = await GetProvider(provider).GetEpisodesAsync(showId, CancellationToken.None);
		return [.. episodes.OrderBy(x => x.Number).Select(x => new ProviderEpisodeItem(x.Id, x.Number))];
	}

	[AgentTool("get_provider_streams")]
	[Description("The streams (qualities, mirrors...) of an episode on an anime provider, the provider's preferred one first.")]
	public async Task<IReadOnlyList<string>> GetProviderStreamsAsync(string provider, string showId, string episodeId, float episodeNumber)
	{
		var servers = await GetProvider(provider).GetServersAsync(new ProviderEpisode(showId, episodeId, episodeNumber), CancellationToken.None);
		return [.. servers.Select(x => x.Name)];
	}

	[AgentTool("play_from_provider")]
	[Description("Plays an episode of a show on any anime provider (from search_provider and get_provider_episodes), whatever provider and " +
				 "show an anime's settings say. Tracked as an anime from the database when its id is given.")]
	public async Task<PlayResult> PlayFromProviderAsync(
		string provider,
		string showId,
		string showTitle,
		string episodeId,
		float episodeNumber,
		[Description("The stream's name, from get_provider_streams; leave it out for the provider's preferred one.")] string? stream = null,
		[Description("The anime (in the database) to track it as: resuming where the user left off, saving progress and marking the episode " +
					 "watched on their list when finished. Leave it out to play it untracked.")] long? animeId = null,
		[Description("How the provider numbers the show's episodes, to tell which of the anime's episodes it is when tracked: Season (each " +
					 "season from 1), Absolute (on across seasons, e.g. a second season from 29), or leave it out to work it out.")] EpisodeNumbering? numbering = null)
	{
		var anime = animeId is { } id ? await lookup.GetAsync(id) : new AnimeModel { Title = showTitle };
		var (source, episode, server, number) = await FindStreamAsync(provider, showId, episodeId, episodeNumber, stream, animeId is null ? null : anime, numbering);
		var status = await watch.StartWatchingFromAsync(anime, number, source, episode, server, track: animeId is not null, s_startTimeout);
		var started = status.IsActive && status.Kind is PlaybackStatusKind.Playing or PlaybackStatusKind.Progress;
		return new PlayResult(started, status.IsActive ? status.Message : "It was cancelled.");
	}

	[AgentTool("download_episodes")]
	[Description("Downloads episodes of an anime from its provider (or another) to the download folder, one after the other in the background " +
				 "(get_downloads shows how they go). Episodes already in the folder aren't downloaded again.")]
	public async Task<IReadOnlyList<DownloadInfo>> DownloadEpisodesAsync(
		long animeId,
		[Description("The first episode to download.")] int fromEpisode,
		[Description("The last episode to download; leave it out for just the first.")] int? toEpisode = null,
		[Description("The provider to download from; leave it out for the anime's.")] string? provider = null,
		[Description("Provider settings for this download only, over the anime's (or the provider's): a JSON object of setting names " +
					 "(from get_module_settings) to values, e.g. {\"Quality\": \"720p\"}.")] string? providerSettings = null)
	{
		if (toEpisode < fromEpisode)
		{
			throw new AgentToolException("toEpisode can't be before fromEpisode.");
		}

		var anime = await lookup.GetAsync(animeId);
		var source = GetProvider(anime, provider);
		if (providerSettings is not null)
		{
			var settings = watch.CopyProviderSettings(anime, source.Id) ?? throw new AgentToolException($"{source.Name} has no settings.");
			ApplySettings(settings, providerSettings);
			source.UseSettings(settings);
		}

		var (show, candidates) = await watch.FindShowAsync(source, anime, CancellationToken.None);
		if (show is null)
		{
			throw new AgentToolException(candidates.Count == 0
				? $"{source.Name} has nothing called {anime.Title}: download it from another provider, or find it there by another title with find_provider_shows."
				: $"It isn't clear which {source.Name} show {anime.Title} is: pick one from find_provider_shows with set_provider_show, then download again.");
		}

		return [.. (await downloads.DownloadEpisodesAsync(anime, fromEpisode, toEpisode ?? fromEpisode, source)).Select(ToInfo)];
	}

	[AgentTool("download_from_provider")]
	[Description("Downloads an episode of a show on any anime provider (from search_provider and get_provider_episodes) to the download folder, in the background. " +
				 "Give the anime it is (in the database) so its ids are kept with it and the episode numbered as the anime's.")]
	public async Task<DownloadInfo> DownloadFromProviderAsync(
		string provider,
		string showId,
		string showTitle,
		string episodeId,
		float episodeNumber,
		[Description("The stream's name, from get_provider_streams; leave it out for the provider's preferred one.")] string? stream = null,
		long? animeId = null,
		[Description("How the provider numbers the show's episodes, as for play_from_provider.")] EpisodeNumbering? numbering = null)
	{
		var anime = animeId is { } id ? await lookup.GetAsync(id) : null;
		var (source, _, server, number) = await FindStreamAsync(provider, showId, episodeId, episodeNumber, stream, anime, numbering);
		return ToInfo(downloads.Download(anime, anime?.Title ?? showTitle, number, server, source.Name));
	}

	[AgentTool("get_downloads")]
	[Description("The episodes downloaded or being downloaded since the app started, in the order they download, and the folder they go to.")]
	public Downloads GetDownloads() => new(preferences.DownloadFolder, [.. downloads.Downloads.Select(ToInfo)]);

	[AgentTool("open_download_folder")]
	[Description("Shows the download folder (a folder in it for each anime) to the user in File Explorer.")]
	public string OpenDownloadFolder()
	{
		downloads.OpenFolder();
		return $"Opened {preferences.DownloadFolder}.";
	}

	[AgentTool("cancel_download")]
	[Description("Stops downloading an episode.")]
	public string CancelDownload([Description("The download's title, from get_downloads.")] string title)
	{
		var item = downloads.Downloads.FirstOrDefault(x => x.IsActive && x.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
				   ?? throw new AgentToolException($"Nothing called {title} is downloading; get_downloads lists them.");
		item.Cancel();
		return $"Cancelled {item.Title}.";
	}

	[AgentTool("get_downloaded_anime")]
	[Description("The anime in the download folder, each in its own folder, with what its info says: the titles and AniList and MyAnimeList ids " +
				 "the Downloads provider finds it by. One without info, or ids, is only found by title.")]
	public IReadOnlyList<DownloadedAnimeInfo> GetDownloadedAnime()
	{
		downloads.RefreshLibrary();
		return [.. downloads.Library.Select(x => new DownloadedAnimeInfo(x.Name, x.Info?.Title, x.Info?.OtherTitles ?? [], x.Info?.AnilistId, x.Info?.MalId,
																		  x.Episodes, DownloadService.Size(x.Bytes) ?? ""))];
	}

	[AgentTool("edit_downloaded_anime")]
	[Description("Changes (or adds) the info of an anime in the download folder: what the Downloads provider finds it by. " +
				 "What's left out stays as it is. Get its ids with search_anime or get_anime.")]
	public string EditDownloadedAnime(
		[Description("The anime's folder, from get_downloaded_anime.")] string folder,
		[Description("Its title; required when it has no info yet.")] string? title = null,
		[Description("Its other titles (romaji, English, synonyms), replacing the ones it has.")] IReadOnlyList<string>? otherTitles = null,
		[Description("Its AniList id; 0 to remove it.")] long? anilistId = null,
		[Description("Its MyAnimeList id; 0 to remove it.")] long? malId = null)
	{
		downloads.RefreshLibrary();
		var anime = downloads.Library.FirstOrDefault(x => x.Name.Equals(folder, StringComparison.OrdinalIgnoreCase))
					?? throw new AgentToolException($"There's no folder called {folder}; get_downloaded_anime lists them.");
		var info = anime.Info ?? new Core.Modules.Streaming.DownloadInfo(title ?? throw new AgentToolException("It has no info yet: give its title."), []);
		try
		{
			downloads.SaveInfo(anime.Name, info with
			{
				Title = title ?? info.Title,
				OtherTitles = otherTitles ?? info.OtherTitles,
				AnilistId = anilistId ?? info.AnilistId,
				MalId = malId ?? info.MalId,
			});
		}
		catch (DownloadException ex)
		{
			throw new AgentToolException(ex.Message);
		}

		return $"Saved {anime.Name}'s info.";
	}

	[AgentTool("delete_downloaded_anime", RequiresApproval = true)]
	[Description("Deletes an anime's folder in the download folder, with all its episodes (to the Recycle Bin).")]
	public string DeleteDownloadedAnime([Description("The anime's folder, from get_downloaded_anime.")] string folder)
	{
		try
		{
			downloads.Delete(folder);
		}
		catch (DownloadException ex)
		{
			throw new AgentToolException(ex.Message);
		}

		return $"Moved {folder} to the Recycle Bin.";
	}

	private static DownloadInfo ToInfo(DownloadItem item) =>
		new(item.Title, item.State, item.Progress < 0 ? null : (int)(item.Progress * 100), item.Message, item.Source, item.FilePath.Length > 0 ? item.FilePath : item.Folder);

	/// <summary>A stream of a provider's episode, and which of the anime's episodes it is (the provider's number without an anime).</summary>
	private async Task<(IAnimeProvider Provider, ProviderEpisode Episode, VideoServer Server, int Number)> FindStreamAsync(
		string provider, string showId, string episodeId, float episodeNumber, string? stream, AnimeModel? anime, EpisodeNumbering? numbering)
	{
		var source = GetProvider(provider);
		var episode = new ProviderEpisode(showId, episodeId, episodeNumber);
		var servers = await source.GetServersAsync(episode, CancellationToken.None);
		var server = (stream is null ? servers.FirstOrDefault() : servers.FirstOrDefault(x => x.Name.Equals(stream, StringComparison.OrdinalIgnoreCase)))
					 ?? throw new AgentToolException(servers.Count == 0
						 ? $"{source.Name} has no streams for that episode."
						 : $"There's no stream called {stream}; they are {string.Join(", ", servers.Select(x => x.Name))}.");
		var number = anime is null
			? (int)episodeNumber
			: await watch.GetAnimeEpisodeAsync(anime, await source.GetEpisodesAsync(showId, CancellationToken.None), episode, numbering, CancellationToken.None);
		return (source, episode, server, number);
	}

	private IAnimeProvider GetProvider(string name)
	{
		var all = providers.CreateAll().ToList();
		return all.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
			   ?? throw new AgentToolException($"There's no provider called {name}; the providers are {string.Join(", ", all.Select(x => x.Name))}.");
	}

	/// <summary>The provider called <paramref name="name"/>, or (null) the anime's, set to use its own settings for it.</summary>
	private IAnimeProvider GetProvider(AnimeModel anime, string? name)
	{
		if (name is null)
		{
			return watch.GetProvider(anime) ?? throw new AgentToolException("There's no anime provider to watch from.");
		}

		var provider = GetProvider(name);
		if (watch.CopyProviderSettings(anime, provider.Id) is { } settings)
		{
			provider.UseSettings(settings);
		}

		return provider;
	}

	/// <summary>Sets the settings named in <paramref name="json"/> (an object of names to values) on <paramref name="settings"/>.</summary>
	private static void ApplySettings(DataContainerConfig settings, string json)
	{
		JsonObject changes;
		try
		{
			changes = JsonNode.Parse(json) as JsonObject ?? throw new AgentToolException("providerSettings must be a JSON object.");
		}
		catch (JsonException ex)
		{
			throw new AgentToolException($"providerSettings isn't JSON: {ex.Message}");
		}

		foreach (var (name, value) in changes)
		{
			var property = settings.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
						   ?? throw new AgentToolException($"There's no setting called {name}; get_module_settings lists them.");
			try
			{
				property.SetValue(settings, value.Deserialize(property.PropertyType, s_settingOptions));
			}
			catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
			{
				throw new AgentToolException($"{value?.ToJsonString()} isn't a value {name} can have.");
			}
		}
	}

	private static ShowCandidate ToCandidate(ProviderShow show) => new(show.Id, show.Title, Math.Round(show.Score, 2));
}
