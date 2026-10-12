using System.ComponentModel;
using System.Globalization;
using Totoro.Core.Agent;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Services;

namespace Totoro.Agent;

public enum DiscoverKind
{
	[Description("Airing now (this season), trending first")] ThisSeason,
	[Description("Starting next season")] NextSeason,
	[Description("Airing an episode today")] AiringToday,
}

/// <summary>What get_user_list sorts by.</summary>
public enum ListSort
{
	[Description("When the user last changed its entry")] LastUpdated,
	[Description("Its number of episodes")] Episodes,
	[Description("How many episodes the user has watched")] WatchedEpisodes,
	[Description("The user's score")] UserScore,
	[Description("Its average score")] AverageScore,
	[Description("When it aired")] Year,
	Popularity,
	[Description("When the user started it")] StartDate,
	[Description("When the user finished it")] FinishDate,
}

/// <summary>What get_list_stats groups by.</summary>
public enum ListGrouping
{
	[Description("No groups: just the totals")] None,
	Status,
	[Description("An anime counts in each of its genres")] Genre,
	Format,
	Year,
	Season,
}

/// <summary>Agent tools for finding anime and managing the user's list.</summary>
public sealed class AnimeTools(
	AnimeLookup lookup,
	IFactory<IAnimeTracker, Guid> trackers,
	AnimeTrackingService tracking,
	IPlaybackProgressStore progress,
	AnimeSongService songs,
	ISongPlayer songPlayer)
{
	[AgentTool("search_anime")]
	[Description("Searches the anime database. Give at least one filter; results are best match first.")]
	public async Task<IReadOnlyList<AnimeSummary>> SearchAnimeAsync(
		[Description("Words from its title, in English or Japanese (romaji), as the user wrote them: typos are forgiven.")] string? title = null,
		[Description("Genres it must have, from get_genres.")] string[]? genres = null,
		[Description("Genres it must not have.")] string[]? excludedGenres = null,
		[Description("The season it aired in; use with a year.")] AnimeSeason? season = null,
		[Description("The earliest year it started airing.")] int? fromYear = null,
		[Description("The latest year it started airing.")] int? toYear = null,
		[Description("The lowest average score, out of 10.")] float? minimumScore = null,
		[Description("How many results to return, at most.")] int limit = 10)
	{
		var request = new AdvancedSearchRequest
		{
			Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
			IncludedGenres = genres is { Length: > 0 } ? [.. genres] : null,
			ExcludedGenres = excludedGenres is { Length: > 0 } ? [.. excludedGenres] : null,
			SeasonName = season,
			MinYear = fromYear,
			MaxYear = toYear,
			MinimumScore = minimumScore,
		};
		if (request.IsEmpty())
		{
			throw new AgentToolException("Give at least a title, genre, season, year or score to search for.");
		}

		var results = await lookup.Database.FindAnimeAsync(request, trackers.CreateDefault());
		if (results.Count == 0 && request.Title is { } searched)
		{
			// A typo the search couldn't get past: the agent likely knows the title's spelling.
			throw new AgentToolException($"No anime matches \"{searched}\"; it's probably misspelt. Don't ask the user: work out the title they meant and search again now with it spelt correctly, in romaji or English.");
		}

		return [.. results.Take(Math.Clamp(limit, 1, 50)).Select(AnimeSummary.From)];
	}

	[AgentTool("discover_anime")]
	[Description("Lists anime airing this season, starting next season, or airing an episode today. Each says whether it's on the user's list (listEntry).")]
	public async Task<IReadOnlyList<AnimeSummary>> DiscoverAnimeAsync(
		DiscoverKind kind,
		[Description("Only when the user asks about their list: true for only anime on it, false for only those not on it. Leave it out otherwise.")] bool? onList = null,
		int limit = 20)
	{
		var database = lookup.Database;
		var anime = kind switch
		{
			DiscoverKind.NextSeason => await database.GetUpcomingAnimeAsync(CancellationToken.None),
			DiscoverKind.AiringToday => await database.GetAiringTodayAsync(CancellationToken.None),
			_ => await database.GetPopularAnimeAsync(CancellationToken.None),
		};
		return [.. anime.Where(x => onList is null || IsOnList(x) == onList).Take(Math.Clamp(limit, 1, 50)).Select(AnimeSummary.From)];
	}

	private static bool IsOnList(AnimeModel anime) => anime.Tracking?.Status is not null;

	[AgentTool("get_genres")]
	[Description("The genres search_anime accepts.")]
	public Task<List<string>> GetGenresAsync() => lookup.Database.GetGenresAsync();

	[AgentTool("get_user_list")]
	[Description("Anime on the user's list (what they're watching, plan to watch, have completed...), with their progress and scores: filtered, sorted, and the first few of them. E.g. their longest completed anime: status Completed, sortBy Episodes, limit 1. For counts and totals, use get_list_stats.")]
	public async Task<ListEntries> GetUserListAsync(
		[Description("Only anime with this status. Watching includes rewatching.")] ListItemStatus? status = null,
		[Description("Only anime with this genre, e.g. Romance.")] string? genre = null,
		[Description("Only anime of this format, e.g. Movie.")] AnimeMediaFormat? format = null,
		[Description("Only anime that started airing in or after this year.")] int? fromYear = null,
		[Description("Only anime that started airing in or before this year.")] int? toYear = null,
		[Description("What to sort them by; LastUpdated (what they changed last) if left out.")] ListSort sortBy = ListSort.LastUpdated,
		[Description("Highest first (true) or lowest first (false).")] bool descending = true,
		[Description("How many to return, at most.")] int limit = 10)
	{
		var list = Filter(await GetListAsync(), status, genre, format, fromYear, toYear).ToList();
		var sorted = Sort(list, sortBy, descending);
		return new ListEntries(list.Count, [.. sorted.Take(Math.Clamp(limit, 1, 50)).Select(AnimeSummary.From)]);
	}

	[AgentTool("get_list_stats")]
	[Description("Counts the anime on the user's list, in groups (e.g. by genre: how many romance anime, and so on), with the episodes watched and their average score in each, biggest group first; and the totals.")]
	public async Task<ListStats> GetListStatsAsync(
		[Description("What to group them by; None for just the totals.")] ListGrouping groupBy = ListGrouping.None,
		[Description("Only anime with this status, e.g. Completed for what they've finished.")] ListItemStatus? status = null,
		[Description("Only anime with this genre.")] string? genre = null,
		[Description("Only anime of this format.")] AnimeMediaFormat? format = null,
		[Description("How many groups to return, at most.")] int limit = 15)
	{
		var list = Filter(await GetListAsync(), status, genre, format, null, null).ToList();
		if (groupBy is ListGrouping.None)
		{
			return ListStats.From(list, []);
		}

		var groups = list.SelectMany(anime => KeysOf(anime, groupBy).Select(key => (Key: key, Anime: anime)))
						 .GroupBy(x => x.Key, x => x.Anime)
						 .Select(x => ListGroup.From(x.Key, [.. x]))
						 .OrderByDescending(x => x.Anime)
						 .ThenBy(x => x.Name)
						 .Take(Math.Clamp(limit, 1, 100));
		return ListStats.From(list, [.. groups]);
	}

	[AgentTool("find_new_seasons")]
	[Description("New seasons (sequels) of anime on the user's list that start airing in a season, this one by default. E.g. whether anime they've completed have a new season this season: status Completed.")]
	public async Task<ListEntries> FindNewSeasonsAsync(
		[Description("Only sequels of anime with this status on their list, e.g. Completed; any if left out.")] ListItemStatus? status = null,
		[Description("The season they start in; this one if left out.")] AnimeSeason? season = null,
		[Description("The season's year; this year if left out.")] int? year = null)
	{
		var onList = Filter(await GetListAsync(), status, null, null, null, null).Select(x => x.Id).ToHashSet();
		var now = Season.Of(DateTime.Now);
		var seasonAnime = await lookup.Database.GetSeasonAnimeAsync(new Season(season ?? now.SeasonName, year ?? now.Year), CancellationToken.None);
		// A new season is a sequel: its prequel is on the list.
		var sequels = seasonAnime.Where(x => x.Related.Any(r => r.Relation == AnimeRelationType.Prequel && onList.Contains(r.Id))).ToList();
		return new ListEntries(sequels.Count, [.. sequels.Select(AnimeSummary.From)]);
	}

	private async Task<List<AnimeModel>> GetListAsync()
	{
		var tracker = trackers.CreateDefault() ?? throw new AgentToolException("No anime database is set up.");
		return await tracker.GetUserListAsync(CancellationToken.None);
	}

	private static IEnumerable<AnimeModel> Filter(IEnumerable<AnimeModel> list, ListItemStatus? status, string? genre, AnimeMediaFormat? format, int? fromYear, int? toYear)
	{
		return list.Where(x => status is null || x.Tracking?.Status == status ||
							   status == ListItemStatus.Watching && x.Tracking?.Status == ListItemStatus.Rewatching)
				   .Where(x => string.IsNullOrWhiteSpace(genre) || x.Genres.Contains(genre.Trim(), StringComparer.OrdinalIgnoreCase))
				   .Where(x => format is null || x.MediaFormat == format)
				   .Where(x => fromYear is null || x.Season?.Year >= fromYear)
				   .Where(x => toYear is null || x.Season is { Year: > 1900 } && x.Season.Year <= toYear);
	}

	private static IEnumerable<AnimeModel> Sort(IEnumerable<AnimeModel> list, ListSort sortBy, bool descending)
	{
		// Anime without the value go last either way.
		Func<AnimeModel, double?> key = sortBy switch
		{
			ListSort.Episodes => x => x.TotalEpisodes is > 0 ? x.TotalEpisodes : null,
			ListSort.WatchedEpisodes => x => x.Tracking?.WatchedEpisodes,
			ListSort.UserScore => x => x.Tracking?.Score is > 0 ? x.Tracking.Score : null,
			ListSort.AverageScore => x => x.MeanScore is > 0 ? x.MeanScore : null,
			ListSort.Year => x => x.Season is { Year: > 1900 } season ? season.Year * 10 + (int)season.SeasonName : null,
			ListSort.Popularity => x => x.Popularity,
			ListSort.StartDate => x => x.Tracking?.StartDate?.Ticks,
			ListSort.FinishDate => x => x.Tracking?.FinishDate?.Ticks,
			_ => x => x.Tracking?.UpdatedAt?.Ticks,
		};
		var known = list.Where(x => key(x) is not null);
		return [.. (descending ? known.OrderByDescending(key) : known.OrderBy(key)), .. list.Where(x => key(x) is null)];
	}

	private static IEnumerable<string> KeysOf(AnimeModel anime, ListGrouping groupBy) => groupBy switch
	{
		ListGrouping.Genre => anime.Genres,
		ListGrouping.Format => [anime.MediaFormat.ToString()],
		ListGrouping.Year => [anime.Season is { Year: > 1900 } season ? season.Year.ToString(CultureInfo.InvariantCulture) : "Unknown"],
		ListGrouping.Season => [anime.Season is { Year: > 1900 } season ? $"{season.SeasonName} {season.Year}" : "Unknown"],
		_ => [anime.Tracking?.Status?.ToString() ?? "Unknown"],
	};

	[AgentTool("get_anime")]
	[Description("Everything about an anime: synopsis, studios, related anime, and the user's list entry.")]
	public async Task<AnimeDetails> GetAnimeAsync(long animeId) => AnimeDetails.From(await lookup.GetAsync(animeId));

	[AgentTool("get_episodes")]
	[Description("An anime's aired episodes, with which the user has watched and where unfinished ones resume from.")]
	public async Task<IReadOnlyList<EpisodeSummary>> GetEpisodesAsync(long animeId)
	{
		var anime = await lookup.GetAsync(animeId);
		var episodes = await anime.GetEpisodesAsync();
		var watched = anime.Tracking?.WatchedEpisodes ?? 0;
		var saved = progress.GetAll(anime);
		return [.. episodes.Select(x => EpisodeSummary.From(x, watched, x.IsSpecial ? null : saved.GetValueOrDefault(x.EpisodeNumber)))];
	}

	[AgentTool("get_anime_songs")]
	[Description("An anime's songs: its openings, endings and insert songs, with who sings them and the episodes they're in. Each may have its audio and a creditless video, which play_anime_song plays. The user sees the songs returned, so ask for just the ones they asked about.")]
	public async Task<AnimeSongList> GetAnimeSongsAsync(
		long animeId,
		[Description("Only songs of this kind, e.g. Opening when the user asks for the openings.")] AnimeSongKind? kind = null,
		[Description("Only the song with this number, when the user asks for one, e.g. 2 for the second opening; use with kind. Leave it out for all of that kind.")] int? number = null)
	{
		var anime = await lookup.GetAsync(animeId);
		var all = await songs.GetSongsAsync(anime);
		if (kind is null)
		{
			return AnimeSongList.From(anime, all);
		}

		return AnimeSongList.From(anime, number is null ? [.. all.Where(x => x.Kind == kind)] : [FindSong(anime, all, kind.Value, number)]);
	}

	[AgentTool("play_anime_song")]
	[Description("Plays one of an anime's songs: its audio in the app (the Music tab), or its creditless video in the media player.")]
	public async Task<SongPlaying> PlayAnimeSongAsync(
		long animeId,
		AnimeSongKind kind,
		[Description("Its number from get_anime_songs; leave it out for the first (or only) one.")] int? number = null,
		[Description("Play its video in the media player rather than its audio in the app.")] bool video = false)
	{
		var anime = await lookup.GetAsync(animeId);
		var song = FindSong(anime, await songs.GetSongsAsync(anime), kind, number);
		try
		{
			if (video)
			{
				songs.PlayVideo(anime, song);
			}
			else
			{
				await songs.PlayAudioAsync(song);
			}
		}
		catch (InvalidOperationException ex)
		{
			throw new AgentToolException(ex.Message);
		}

		return SongPlaying.From(anime, song, video);
	}

	/// <summary>
	/// The song of that kind with that number; without a number, the first. A lone song has no number, so "opening 1" is
	/// it too.
	/// </summary>
	private static AnimeSong FindSong(AnimeModel anime, IReadOnlyList<AnimeSong> all, AnimeSongKind kind, int? number)
	{
		var ofKind = all.Where(x => x.Kind == kind).ToList();
		return (number is null ? ofKind.FirstOrDefault() : ofKind.FirstOrDefault(x => x.Number == number))
			?? (number == 1 && ofKind.Count == 1 ? ofKind[0] : null)
			?? throw new AgentToolException(all.Count == 0
				? $"{anime.Title} has no songs on AnimeThemes."
				: $"{anime.Title} has no such song. It has: {string.Join(", ", all.Select(x => $"{x.Label} ({x.Title})"))}.");
	}

	[AgentTool("stop_anime_song")]
	[Description("Stops the song playing in the app (from play_anime_song or the Music tab).")]
	public string StopAnimeSong()
	{
		if (songPlayer.Current is not { } song)
		{
			return "No song is playing.";
		}

		songPlayer.Stop();
		return $"Stopped {song.Label}: {song.Title}.";
	}

	[AgentTool("update_list_entry")]
	[Description("Adds an anime to the user's list or changes its entry. Only the values given change. Returns the entry as it is now.")]
	public async Task<ListEntry?> UpdateListEntryAsync(
		long animeId,
		ListItemStatus? status = null,
		[Description("How many episodes they've watched.")] int? watchedEpisodes = null,
		[Description("Their score, from 1 to 10; 0 clears it.")] int? score = null,
		DateOnly? startDate = null,
		DateOnly? finishDate = null)
	{
		if (status is null && watchedEpisodes is null && score is null && startDate is null && finishDate is null)
		{
			throw new AgentToolException("Give at least one value to change.");
		}

		if (score is < 0 or > 10)
		{
			throw new AgentToolException("The score is from 0 to 10.");
		}

		var anime = await lookup.GetAsync(animeId);
		if (watchedEpisodes is { } watched && (watched < 0 || anime.TotalEpisodes is > 0 and var total && watched > total))
		{
			throw new AgentToolException($"It has {anime.TotalEpisodes} episodes.");
		}

		var saved = await tracking.UpdateAsync(anime, new Tracking
		{
			// Something new to the list needs a status: watching if they've started it, otherwise planning to.
			Status = status ?? (anime.Tracking?.Status is not null ? null
				: watchedEpisodes is > 0 ? ListItemStatus.Watching
				: ListItemStatus.PlanToWatch),
			WatchedEpisodes = watchedEpisodes,
			Score = score,
			StartDate = startDate?.ToDateTime(TimeOnly.MinValue),
			FinishDate = finishDate?.ToDateTime(TimeOnly.MinValue),
		});
		return ListEntry.From(saved);
	}

	[AgentTool("remove_from_list", RequiresApproval = true)]
	[Description("Removes an anime from the user's list, with their progress and score on it.")]
	public async Task<string> RemoveFromListAsync(long animeId)
	{
		var anime = await lookup.GetAsync(animeId);
		return await tracking.RemoveAsync(anime) ? $"Removed {anime.Title} from the list." : $"{anime.Title} wasn't on the list.";
	}
}
