using System.Globalization;
using System.Text.RegularExpressions;
using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Relations;

/// <summary>An anime's ids in each database, as anime-relations rules give them; null where unknown ("?").</summary>
public sealed record RelationIds(long? MyAnimeList, long? Kitsu, long? AniList)
{
	/// <summary>The anime's ids, as far as it knows them (Kitsu's never).</summary>
	public static RelationIds Of(AnimeModel anime) => new(
		anime.ExternalIds.MyAnimeList > 0 ? anime.ExternalIds.MyAnimeList : null,
		null,
		anime.ExternalIds.Anilist > 0 ? anime.ExternalIds.Anilist : null);

	/// <summary>Whether it's the same anime: any id both know matches.</summary>
	public bool Matches(RelationIds other)
	{
		return MyAnimeList is { } mal && mal == other.MyAnimeList ||
			   AniList is { } anilist && anilist == other.AniList ||
			   Kitsu is { } kitsu && kitsu == other.Kitsu;
	}

	public override string ToString() => $"{Format(MyAnimeList)}|{Format(Kitsu)}|{Format(AniList)}";

	private static string Format(long? id) => id?.ToString(CultureInfo.InvariantCulture) ?? "?";
}

/// <summary>Episodes <see cref="First"/> to <see cref="Last"/>; no last means it runs on ("?").</summary>
public sealed record EpisodeRange(int First, int? Last)
{
	public bool Contains(int episode) => episode >= First && (Last is not { } last || episode <= last);

	public override string ToString() => Last == First ? $"{First}" : $"{First}-{Last?.ToString(CultureInfo.InvariantCulture) ?? "?"}";
}

/// <summary>
/// A rule from anime-relations (https://github.com/erengy/anime-relations): episodes <see cref="SourceEpisodes"/> of
/// <see cref="Source"/> are episodes <see cref="DestinationEpisodes"/> of <see cref="Destination"/>. E.g. "Seihantai na
/// Kimi to Boku 13-25 are ~ 2nd Season 1-13": a provider using absolute numbering from the first season calls the 2nd
/// season's first episode its 13th.
/// </summary>
/// <param name="AlsoCountsOnItself">
/// The rule's "!": the destination's own show may use the same absolute numbering (e.g. a "2nd Season" show whose
/// episodes are 13-25), which is the same mapping with the destination as the source. It matters to tools that work out
/// the anime from a file's name (Taiga, anime-relations' own); Totoro looks rules up by the season being watched, where
/// both give the same number, so it's kept for the file's sake only.
/// </param>
public sealed partial record AnimeRelationRule(
	RelationIds Source,
	EpisodeRange SourceEpisodes,
	RelationIds Destination,
	EpisodeRange DestinationEpisodes,
	bool AlsoCountsOnItself = false)
{
	/// <summary>What the rule is about, e.g. "Seihantai na Kimi to Boku -> ~ 2nd Season": the comment above it in the file.</summary>
	public string? Title { get; init; }

	/// <summary>
	/// Where the destination's episode is in the source's numbering (e.g. 2nd season episode 4 -> 16), or null if the rule
	/// doesn't cover it.
	/// </summary>
	public int? ToSourceEpisode(int destinationEpisode)
	{
		return DestinationEpisodes.Contains(destinationEpisode) ? SourceEpisodes.First + destinationEpisode - DestinationEpisodes.First : null;
	}

	/// <summary>The rule's line in an anime-relations file.</summary>
	public string ToLine() => $"- {Source}:{SourceEpisodes} -> {Destination}:{DestinationEpisodes}{(AlsoCountsOnItself ? "!" : "")}";

	/// <summary>Reads a rule line ("- 1|2|3:13-25 -> 4|5|6:1-13!"); null if it isn't one.</summary>
	public static AnimeRelationRule? Parse(string line, string? title = null)
	{
		var match = RuleLine().Match(line.Trim());
		if (!match.Success)
		{
			return null;
		}

		var source = ParseIds(match.Groups["source"].Value, null);
		var destination = ParseIds(match.Groups["destination"].Value, source);
		var sourceEpisodes = ParseRange(match.Groups["sourceEpisodes"].Value);
		var destinationEpisodes = ParseRange(match.Groups["destinationEpisodes"].Value);
		if (source is null || destination is null || sourceEpisodes is null || destinationEpisodes is null)
		{
			return null;
		}

		return new AnimeRelationRule(source, sourceEpisodes, destination, destinationEpisodes, match.Groups["self"].Success) { Title = title };
	}

	private static RelationIds? ParseIds(string text, RelationIds? source)
	{
		var parts = text.Split('|');
		if (parts.Length != 3)
		{
			return null;
		}

		// "~" repeats the source's id; "?" is unknown.
		long? Id(int index, long? sourceId) => parts[index] switch
		{
			"~" => sourceId,
			"?" => null,
			var value => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null,
		};

		return new RelationIds(Id(0, source?.MyAnimeList), Id(1, source?.Kitsu), Id(2, source?.AniList));
	}

	private static EpisodeRange? ParseRange(string text)
	{
		var parts = text.Split('-');
		if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var first))
		{
			return null;
		}

		if (parts.Length == 1)
		{
			return new EpisodeRange(first, first);
		}

		return parts[1] == "?" ? new EpisodeRange(first, null)
			: int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var last) ? new EpisodeRange(first, last)
			: null;
	}

	[GeneratedRegex(@"^-\s*(?<source>[^:\s]+):(?<sourceEpisodes>[\d?-]+)\s*->\s*(?<destination>[^:\s]+):(?<destinationEpisodes>[\d?-]+)(?<self>!)?$")]
	private static partial Regex RuleLine();
}

/// <summary>An anime-relations file: its rules (with the comment above each as its title) and when it was last changed.</summary>
public sealed record AnimeRelationsFile(IReadOnlyList<AnimeRelationRule> Rules, string? LastModified)
{
	public static AnimeRelationsFile Parse(string text)
	{
		List<AnimeRelationRule> rules = [];
		string? lastModified = null;
		string? title = null;
		foreach (var raw in text.Split('\n'))
		{
			var line = raw.Trim();
			if (line.StartsWith("- last_modified:", StringComparison.Ordinal))
			{
				lastModified = line["- last_modified:".Length..].Trim();
			}
			else if (line.StartsWith('#'))
			{
				title = line.TrimStart('#').Trim();
			}
			else if (AnimeRelationRule.Parse(line, title) is { } rule)
			{
				rules.Add(rule);
			}
			else if (line.Length == 0)
			{
				title = null;
			}
		}

		return new AnimeRelationsFile(rules, lastModified);
	}

	/// <summary>The file's text: each rule under its title, a blank line between rules.</summary>
	public string Format(string header)
	{
		var lines = new List<string> { header, "", "::rules", "" };
		foreach (var rule in Rules)
		{
			if (!string.IsNullOrWhiteSpace(rule.Title))
			{
				lines.Add($"# {rule.Title}");
			}

			lines.Add(rule.ToLine());
			lines.Add("");
		}

		return string.Join(Environment.NewLine, lines);
	}
}
