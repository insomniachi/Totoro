using System.Text;

namespace Totoro.Core.Modules.Anime;

/// <summary>
/// Searching by title, forgiving typos and punctuation ("jujutsu kaisn", "one peice", "journeys end"). The database matches
/// whole words, in any of an anime's titles (English, romaji, synonyms); when that finds nothing, this searches again
/// for each of its longer words alone (a typo is usually in just one), and ranks what those find by how close their
/// titles are to what was typed. A title of one misspelt word can't be found that way, so failing that, the user's own list
/// is searched too, here: what they look for is often on it.
/// </summary>
public static class FuzzyAnimeSearch
{
	// Searches beyond the first, at most (each is a request to the database).
	private const int MaxRetries = 3;

	// How close a title has to be to be a result; and close enough to stop searching.
	private const double MinSimilarity = 0.6;
	private const double GoodSimilarity = 0.85;

	extension(IAnimeMetadataProvider database)
	{
		/// <summary>Searches as <see cref="IAnimeMetadataProvider.SearchAnimeAsync(AdvancedSearchRequest)"/>, forgiving typos in the title.</summary>
		/// <param name="userList">The user's list, to search too if nothing else matches; null not to.</param>
		public async Task<List<AnimeModel>> FindAnimeAsync(AdvancedSearchRequest request, IAnimeTracker? userList = null, CancellationToken ct = default)
		{
			var results = await database.SearchAnimeAsync(request);
			if (results.Count > 0 || Normalize(request.Title ?? "") is not { Length: > 0 } query)
			{
				return results;
			}

			Dictionary<long, (AnimeModel Anime, double Similarity)> found = [];
			foreach (var variant in Variants(query).Take(MaxRetries))
			{
				ct.ThrowIfCancellationRequested();
				foreach (var anime in await database.SearchAnimeAsync(WithTitle(request, variant)))
				{
					found.TryAdd(anime.Id, (anime, Similarity(query, anime)));
				}

				if (found.Values.Any(x => x.Similarity >= GoodSimilarity))
				{
					break;
				}
			}

			// Only for a title alone: the list can't be filtered by the rest.
			if (userList is not null && !found.Values.Any(x => x.Similarity >= MinSimilarity) && WithTitle(request, "").IsEmpty())
			{
				foreach (var anime in await userList.GetUserListAsync(ct))
				{
					found.TryAdd(anime.Id, (anime, Similarity(query, anime)));
				}
			}

			return
			[
				.. found.Values.Where(x => x.Similarity >= MinSimilarity)
						.OrderByDescending(x => x.Similarity)
						.ThenByDescending(x => x.Anime.Popularity)
						.Select(x => x.Anime)
			];
		}
	}

	/// <summary>What to search for instead: its longer words, each alone, longest first.</summary>
	private static IEnumerable<string> Variants(string query)
	{
		var words = query.Split(' ');
		return words.Length < 2 ? [] : words.Where(x => x.Length >= 3).Distinct().OrderByDescending(x => x.Length);
	}

	private static AdvancedSearchRequest WithTitle(AdvancedSearchRequest request, string title) => new()
	{
		Title = title,
		SeasonName = request.SeasonName,
		Source = request.Source,
		IncludedGenres = request.IncludedGenres,
		ExcludedGenres = request.ExcludedGenres,
		MinimumScore = request.MinimumScore,
		MaximumScore = request.MaximumScore,
		MinYear = request.MinYear,
		MaxYear = request.MaxYear,
	};

	/// <summary>
	/// How close the anime's closest title is to the query, from 0 to 1: the whole title, its beginning (for a query
	/// that's the start of one), or its words (for a query that's some of them, e.g. "polar oposites").
	/// </summary>
	private static double Similarity(string query, AnimeModel anime)
	{
		string[] titles = [anime.Title, anime.EngTitle, anime.RomajiTitle, .. anime.AlternateTitles];
		return titles.Select(Normalize).Where(x => x.Length > 0).Select(title => Math.Max(
						 Math.Max(Ratio(query, title), Ratio(query, title[..Math.Min(title.Length, query.Length)])),
						 WordsFound(query, title)))
					 .DefaultIfEmpty(0)
					 .Max();
	}

	/// <summary>How many of the query's words are (nearly) in the title, as a fraction of them.</summary>
	private static double WordsFound(string query, string title)
	{
		var titleWords = title.Split(' ');
		var queryWords = query.Split(' ');
		return queryWords.Count(word => titleWords.Any(x => Ratio(word, x) >= 0.75)) / (double)queryWords.Length;
	}

	/// <summary>1 for the same text, less for each letter added, removed or changed (Levenshtein).</summary>
	private static double Ratio(string a, string b)
	{
		var longest = Math.Max(a.Length, b.Length);
		return longest == 0 ? 1 : 1 - (double)Distance(a, b) / longest;
	}

	private static int Distance(string a, string b)
	{
		var previous = new int[b.Length + 1];
		var current = new int[b.Length + 1];
		for (var j = 0; j <= b.Length; j++)
		{
			previous[j] = j;
		}

		for (var i = 1; i <= a.Length; i++)
		{
			current[0] = i;
			for (var j = 1; j <= b.Length; j++)
			{
				var cost = a[i - 1] == b[j - 1] ? 0 : 1;
				current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
			}

			(previous, current) = (current, previous);
		}

		return previous[b.Length];
	}

	/// <summary>Lower case letters and digits, one space between words; apostrophes dropped ("Journey's" as "journeys").</summary>
	private static string Normalize(string text)
	{
		var normalized = new StringBuilder(text.Length);
		foreach (var c in text.ToLowerInvariant())
		{
			if (char.IsLetterOrDigit(c))
			{
				normalized.Append(c);
			}
			else if (c is not ('\'' or '’') && normalized.Length > 0 && normalized[^1] != ' ')
			{
				normalized.Append(' ');
			}
		}

		return normalized.ToString().TrimEnd();
	}
}
