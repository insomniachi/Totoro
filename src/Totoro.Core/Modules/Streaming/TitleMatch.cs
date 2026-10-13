using System.Text.RegularExpressions;

namespace Totoro.Core.Modules.Streaming;

/// <summary>How well a provider's show title matches a search, for <see cref="ProviderShow.Score"/>.</summary>
public static partial class TitleMatch
{
	/// <summary>
	/// 1 for the same title (ignoring case and punctuation); otherwise how many words they share, with a boost when one
	/// contains the other.
	/// </summary>
	public static double Score(string query, string title)
	{
		query = Normalize(query);
		title = Normalize(title);
		if (query == title)
		{
			return 1;
		}

		var queryWords = query.Split(' ');
		var titleWords = title.Split(' ');
		var shared = queryWords.Intersect(titleWords).Count();
		var score = (double)shared / queryWords.Union(titleWords).Count();

		if (query.Length > 0 && title.Length > 0 && (title.Contains(query) || query.Contains(title)))
		{
			score = Math.Max(score, 0.6 + 0.3 * Math.Min(query.Length, title.Length) / Math.Max(query.Length, title.Length));
		}

		return score;
	}

	/// <summary>Lowercase words only: "Sousou no Frieren: Season 2" and "sousou no frieren season 2" match.</summary>
	public static string Normalize(string title)
	{
		return Spaces().Replace(NotLetterOrDigit().Replace(title.ToLowerInvariant(), " "), " ").Trim();
	}

	[GeneratedRegex(@"[^\p{L}\p{N}]")]
	private static partial Regex NotLetterOrDigit();

	[GeneratedRegex(@"\s+")]
	private static partial Regex Spaces();
}
