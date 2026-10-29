using System.ComponentModel;
using System.Globalization;

namespace Totoro.Core.Modules.Anime;

/// <summary>What the user's list is sorted by.</summary>
public enum ListSort
{
	[Description("When the user last changed its entry")] LastUpdated,
	[Description("Its title")] Title,
	[Description("Its number of episodes")] Episodes,
	[Description("How many episodes the user has watched")] WatchedEpisodes,
	[Description("The user's score")] UserScore,
	[Description("Its average score")] AverageScore,
	[Description("When it aired")] Year,
	Popularity,
	[Description("When the user started it")] StartDate,
	[Description("When the user finished it")] FinishDate,
}

/// <summary>
/// Matching and sorting the user's list: what the Library page's filters and sorting do, and the agent's get_user_list,
/// so both find and order the same anime.
/// </summary>
public static class UserListQuery
{
	/// <summary>Whether any of the anime's titles (its own, English, romaji or another) contains <paramref name="query"/>, ignoring case and accents.</summary>
	public static bool MatchesTitle(IMediaModel anime, string? query)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return true;
		}

		var compare = CultureInfo.InvariantCulture.CompareInfo;
		const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
		return new[] { anime.Title, anime.EngTitle, anime.RomajiTitle }.Concat(anime.AlternateTitles)
			.Any(x => !string.IsNullOrEmpty(x) && compare.IndexOf(x, query.Trim(), Options) >= 0);
	}

	/// <summary>Whether it aired in that season and from fromYear to toYear (any left out matches any).</summary>
	public static bool MatchesSeason(AnimeModel anime, AnimeSeason? season, int? fromYear, int? toYear)
	{
		return (season is null || anime.Season?.SeasonName == season && anime.Season.Year > 1900) &&
			   (fromYear is null || anime.Season?.Year >= fromYear) &&
			   (toYear is null || anime.Season is { Year: > 1900 } && anime.Season.Year <= toYear);
	}

	/// <summary>
	/// Orders anime by <paramref name="sort"/>, anime without the value (e.g. no score) last either way; ties keep the order
	/// <paramref name="then"/> gives (e.g. the list's own).
	/// </summary>
	public static IComparer<T> Comparer<T>(ListSort sort, bool descending, IComparer<T>? then = null) where T : IMediaModel
	{
		return System.Collections.Generic.Comparer<T>.Create((a, b) =>
		{
			var result = sort == ListSort.Title ? CompareTitles(a, b, descending) : CompareKeys(Key(a, sort), Key(b, sort), descending);
			return result != 0 || then is null ? result : then.Compare(a, b);
		});
	}

	/// <summary>The anime in <paramref name="sort"/>'s order.</summary>
	public static List<T> Sort<T>(IEnumerable<T> list, ListSort sort, bool descending) where T : IMediaModel
	{
		var sorted = list.ToList();
		sorted.Sort(Comparer<T>(sort, descending));
		return sorted;
	}

	private static int CompareTitles(IMediaModel a, IMediaModel b, bool descending)
	{
		var result = string.Compare(a.Title, b.Title, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);
		return descending ? -result : result;
	}

	private static int CompareKeys(double? a, double? b, bool descending)
	{
		return (a, b) switch
		{
			(null, null) => 0,
			(null, _) => 1,
			(_, null) => -1,
			_ => descending ? b.Value.CompareTo(a.Value) : a.Value.CompareTo(b.Value),
		};
	}

	/// <summary>The value sorted by; null when the item hasn't one (manga have no airing season).</summary>
	private static double? Key(IMediaModel item, ListSort sort) => sort switch
	{
		ListSort.Episodes => item.Total is > 0 ? item.Total : null,
		ListSort.WatchedEpisodes => item.Progress,
		ListSort.UserScore => item.UserScore is > 0 ? item.UserScore : null,
		ListSort.AverageScore => item.MeanScore is > 0 ? item.MeanScore : null,
		ListSort.Year => item is AnimeModel { Season: { Year: > 1900 } season } ? season.Year * 10 + (int)season.SeasonName : null,
		ListSort.Popularity => item.Popularity,
		ListSort.StartDate => item.StartDate?.Ticks,
		ListSort.FinishDate => item.FinishDate?.Ticks,
		_ => item.UpdatedAt?.Ticks,
	};
}
