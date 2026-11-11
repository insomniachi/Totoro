using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Relations;
using Totoro.Core.Settings;

namespace Totoro.Playback;

/// <summary>Matches an anime with a provider's shows, and its episodes with the show's.</summary>
public static class ShowMatching
{
	/// <summary>The provider's shows that may be the anime, best match first; a score of 1 is an exact title match.</summary>
	public static async Task<List<ProviderShow>> SearchShowsAsync(IAnimeProvider provider, AnimeModel anime, CancellationToken ct)
	{
		// A provider that knows the anime by id has the show for sure; else (or for shows it has without ids) search titles.
		if (await provider.FindShowAsync(anime.ExternalIds, ct) is { } byId)
		{
			return [byId with { Score = 1 }];
		}

		// Any of the anime's titles may be the one the provider uses (SubsPlease uses romaji).
		string[] titles = [.. new[] { anime.Title, anime.RomajiTitle, anime.EngTitle }.Concat(anime.AlternateTitles)
																							.Where(x => !string.IsNullOrWhiteSpace(x))
																							.Distinct(StringComparer.OrdinalIgnoreCase)];
		var matches = new Dictionary<string, ProviderShow>();
		foreach (var title in titles)
		{
			foreach (var found in await provider.SearchAsync(title, ct))
			{
				var show = found with { Score = IdScore(found, anime.ExternalIds) };
				if (!matches.TryGetValue(show.Id, out var existing) || existing.Score < show.Score)
				{
					matches[show.Id] = show;
				}
			}

			// Its ids say it's the anime: the other titles needn't be searched.
			if (matches.Values.Any(x => x.Score >= 1 && x.Ids is not null))
			{
				break;
			}
		}

		return [.. matches.Values.OrderByDescending(x => x.Score)];
	}

	/// <summary>
	/// The show's score as the anime: 1 when an id both have is the same, never 1 (it's not the anime) when it differs;
	/// its title's score when they've no id in common.
	/// </summary>
	internal static double IdScore(ProviderShow show, AnimeId anime)
	{
		bool? same = show.Ids is not { } ids ? null
			: ids.Anilist > 0 && anime.Anilist > 0 ? ids.Anilist == anime.Anilist
			: ids.MyAnimeList > 0 && anime.MyAnimeList > 0 ? ids.MyAnimeList == anime.MyAnimeList
			: null;
		return same switch
		{
			true => 1,
			false => Math.Min(show.Score, 0.99),
			null => show.Score,
		};
	}

	/// <summary>The anime's episode that's the provider's <paramref name="episode"/>; see <see cref="WatchService.GetAnimeEpisodeAsync"/>.</summary>
	internal static int AnimeEpisode(List<ProviderEpisode> episodes, AnimeModel anime, ProviderEpisode episode, EpisodeNumbering? numbering,
									 IAnimeRelations relations)
	{
		for (var number = 1; number <= (anime.TotalEpisodes ?? episodes.Count); number++)
		{
			if (PickEpisode(episodes, anime, number, numbering, relations)?.Id == episode.Id)
			{
				return number;
			}
		}

		return (int)episode.Number;
	}

	/// <summary>
	/// The provider's episode for the anime's episode <paramref name="number"/>, skipping specials like 12.5. Providers may
	/// use absolute numbering, carrying on across seasons (one show for both seasons, the second from 13): then
	/// anime-relations (and the user's rules) say where the episode is. <paramref name="numbering"/> is the anime's
	/// setting: by season, absolute, or (null) worked out from the provider's episode numbers.
	/// </summary>
	internal static ProviderEpisode? PickEpisode(List<ProviderEpisode> episodes, AnimeModel anime, int number, EpisodeNumbering? numbering, IAnimeRelations relations)
	{
		var regular = episodes.Where(x => x.Number == MathF.Floor(x.Number)).ToList();
		ProviderEpisode? Numbered(int n) => regular.FirstOrDefault(x => (int)x.Number == n);

		// Absolute numbering (one show for several seasons): anime-relations says where the episode is.
		if (numbering != EpisodeNumbering.Season && relations.FindAbsoluteEpisode(anime, number) is { } absolute)
		{
			// Worked out: absolute if the provider's numbers go past this season's, or it hasn't this season's number.
			var seasonLength = anime.TotalEpisodes ?? absolute.Rule.DestinationEpisodes.Last;
			var isAbsolute = numbering == EpisodeNumbering.Absolute ||
							 (seasonLength is { } length && regular.Any(x => x.Number > length)) ||
							 Numbered(number) is null;
			if (isAbsolute)
			{
				return Numbered(absolute.Episode);
			}
		}

		// Whether anime-relations says this anime carries on from an earlier season.
		var ids = RelationIds.Of(anime);
		var destinationRule = relations.GetRules(anime).FirstOrDefault(x => x.Rule.Destination.Matches(ids))?.Rule;

		var offset = regular.Count > 0 ? (int)regular.Min(x => x.Number) - 1 : 0;
		if (numbering == EpisodeNumbering.Absolute)
		{
			// A first season's absolute numbers are its own. A later one's, from 1, would be an earlier season's episode.
			return offset > 0 || destinationRule is null ? Numbered(number + offset) : null;
		}

		// If this anime is a subsequent season in anime-relations, and the provider has episodes past this season's
		// length (or starting at the absolute episode), the provider is using absolute numbering across seasons.
		// Don't fall back to season numbers (e.g. episode 3 of season 1).
		if (destinationRule is not null && numbering != EpisodeNumbering.Season)
		{
			var seasonLength = anime.TotalEpisodes ?? destinationRule.DestinationEpisodes.Last;
			var isAbsolute = (seasonLength is { } length && regular.Any(x => x.Number > length)) ||
							 regular.Any(x => x.Number >= destinationRule.SourceEpisodes.First);
			if (isAbsolute)
			{
				return null;
			}
		}

		var seasonEpisode = Numbered(number);
		if (seasonEpisode is not null || numbering == EpisodeNumbering.Season)
		{
			return seasonEpisode;
		}

		// No rule: providers that only list a later season's episodes may still use absolute numbering (a second season
		// from 13); count from their first.
		return offset > 0 ? Numbered(number + offset) : null;
	}
}
