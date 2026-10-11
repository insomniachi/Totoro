using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Services;

namespace Totoro.Agent;

public enum DiscoverKind
{
	[Description("Popular this season")] Popular,
	[Description("Starting next season")] Upcoming,
	[Description("Airing an episode today")] AiringToday,
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
		[Description("Words from its title, in English or Japanese (romaji).")] string? title = null,
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

		var results = await lookup.Database.SearchAnimeAsync(request);
		return [.. results.Take(Math.Clamp(limit, 1, 50)).Select(AnimeSummary.From)];
	}

	[AgentTool("discover_anime")]
	[Description("Lists anime that are popular this season, starting next season, or airing an episode today.")]
	public async Task<IReadOnlyList<AnimeSummary>> DiscoverAnimeAsync(DiscoverKind kind, int limit = 20)
	{
		var database = lookup.Database;
		var anime = kind switch
		{
			DiscoverKind.Upcoming => await database.GetUpcomingAnimeAsync(CancellationToken.None),
			DiscoverKind.AiringToday => await database.GetAiringTodayAsync(CancellationToken.None),
			_ => await database.GetPopularAnimeAsync(CancellationToken.None),
		};
		return [.. anime.Take(Math.Clamp(limit, 1, 50)).Select(AnimeSummary.From)];
	}

	[AgentTool("get_genres")]
	[Description("The genres search_anime accepts.")]
	public Task<List<string>> GetGenresAsync() => lookup.Database.GetGenresAsync();

	[AgentTool("get_user_list")]
	[Description("The user's anime list (what they're watching, plan to watch, have completed...), with their progress and scores.")]
	public async Task<IReadOnlyList<AnimeSummary>> GetUserListAsync(
		[Description("Only anime with this status. Watching includes rewatching.")] ListItemStatus? status = null)
	{
		var tracker = trackers.CreateDefault() ?? throw new AgentToolException("No anime database is set up.");
		var list = await tracker.GetUserListAsync(CancellationToken.None);
		return
		[
			.. list.Where(x => status is null || x.Tracking?.Status == status ||
							   status == ListItemStatus.Watching && x.Tracking?.Status == ListItemStatus.Rewatching)
				   .Select(AnimeSummary.From)
		];
	}

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
	[Description("An anime's songs: its openings, endings and insert songs, with who sings them and the episodes they're in. Each may have its audio and a creditless video, which play_anime_song plays.")]
	public async Task<IReadOnlyList<AnimeSong>> GetAnimeSongsAsync(long animeId)
	{
		var anime = await lookup.GetAsync(animeId);
		return await songs.GetSongsAsync(anime);
	}

	[AgentTool("play_anime_song")]
	[Description("Plays one of an anime's songs: its audio in the app (the Music tab), or its creditless video in the media player.")]
	public async Task<string> PlayAnimeSongAsync(
		long animeId,
		AnimeSongKind kind,
		[Description("Its number from get_anime_songs; leave it out if it has none.")] int? number = null,
		[Description("Play its video in the media player rather than its audio in the app.")] bool video = false)
	{
		var anime = await lookup.GetAsync(animeId);
		var song = (await songs.GetSongsAsync(anime)).FirstOrDefault(x => x.Kind == kind && x.Number == number)
			?? throw new AgentToolException($"{anime.Title} has no such song; get_anime_songs lists them.");
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

		return $"Playing {song.Label}: {song.Title}.";
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
