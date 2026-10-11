using Totoro.Core.Relations;

namespace Totoro.ViewModels.Relations;

/// <summary>Words for anime-relations rules, for showing them.</summary>
public static class RelationRuleText
{
	/// <summary>
	/// What the rule is about, e.g. "Seihantai na Kimi to Boku → Seihantai na Kimi to Boku 2nd Season"; its ids if it has
	/// no title. anime-relations' titles write the second anime's as "~ 2nd Season", "~" standing for the first's.
	/// </summary>
	public static string Title(AnimeRelationRule rule)
	{
		if (string.IsNullOrWhiteSpace(rule.Title))
		{
			return $"{rule.Source} → {rule.Destination}";
		}

		var parts = rule.Title.Split("->", 2, StringSplitOptions.TrimEntries);
		return parts.Length == 2 ? $"{parts[0]} → {parts[1].Replace("~", parts[0])}" : rule.Title;
	}

	/// <summary>What it does, e.g. "Episodes 1-13 are numbered 13-25".</summary>
	public static string Effect(AnimeRelationRule rule) => $"Episodes {rule.DestinationEpisodes} are numbered {rule.SourceEpisodes}";
}
