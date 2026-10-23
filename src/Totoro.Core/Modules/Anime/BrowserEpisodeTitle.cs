using System.Text.RegularExpressions;

namespace Totoro.Core.Modules.Anime;

/// <summary>
/// The anime and episode a video playing in a browser is of, from what the site says it is ("Frieren Episode 5 English Sub
/// - HiAnime"): the title, then the episode. Sites that name the episode on its own ("E5 - The Priest's Lie") put the
/// anime's title in the artist instead.
/// </summary>
public static partial class BrowserEpisodeTitle
{
	/// <summary>The anime's title and the episode's number; null when there's no episode number (it isn't an episode).</summary>
	public static (string Title, int Episode)? Parse(string? title, string? artist)
	{
		if (title is null || EpisodeNumber().Match(title) is not { Success: true } match)
		{
			return null;
		}

		var name = Clean(title[..match.Index]);
		if (name.Length == 0)
		{
			name = Clean(artist ?? "");
		}

		return name.Length > 0 && int.TryParse(match.Groups[1].Value, out var episode) && episode > 0 ? (name, episode) : null;
	}

	private static string Clean(string text) => WatchPrefix().Replace(text.Trim(' ', '-', '|', ':', '–', '—', ','), "");

	[GeneratedRegex(@"(?:\bS\d+\s*E|\b(?:Episode|Ep\.?|E))\s*(\d{1,4})\b", RegexOptions.IgnoreCase)]
	private static partial Regex EpisodeNumber();

	[GeneratedRegex(@"^Watch\s+", RegexOptions.IgnoreCase)]
	private static partial Regex WatchPrefix();
}
