using System.ComponentModel;
using System.Net;
using System.Text.RegularExpressions;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;

namespace Totoro.Agent;

// What the agent tools return. Plain records, serialized to JSON for the agent: give them names and [Description]s
// that explain what each value means, and keep them free of UI types.

[Description("An anime, as it appears in search results and lists. Its id is what every other anime tool takes.")]
public sealed record AnimeSummary(
	[property: Description("The anime's id in the selected anime database (AniList or MyAnimeList).")]
	long Id,
	string Title,
	string? EnglishTitle,
	[property: Description("TV, Movie, OVA, ONA, Special, Music or Unknown.")]
	AnimeMediaFormat Format,
	AiringStatus AiringStatus,
	[property: Description("Its number of episodes, if known (unknown while it's airing).")]
	int? Episodes,
	[property: Description("The average score, out of 10.")]
	double? Score,
	[property: Description("When it aired, e.g. \"Fall 2023\".")]
	string? Season,
	IReadOnlyCollection<string> Genres,
	[property: Description("The user's list entry, if it's on their list.")]
	ListEntry? ListEntry)
{
	public static AnimeSummary From(AnimeModel anime) => new(
		anime.Id,
		anime.Title,
		string.IsNullOrWhiteSpace(anime.EngTitle) || anime.EngTitle == anime.Title ? null : anime.EngTitle,
		anime.MediaFormat,
		anime.AiringStatus,
		anime.TotalEpisodes is > 0 ? anime.TotalEpisodes : null,
		anime.MeanScore is > 0 and var score ? Math.Round(score / 10, 1) : null,
		anime.Season is { Year: > 1900 } season ? $"{season.SeasonName} {season.Year}" : null,
		anime.Genres,
		ListEntry.From(anime.Tracking));
}

[Description("The user's list entry for an anime.")]
public sealed record ListEntry(
	ListItemStatus? Status,
	int? WatchedEpisodes,
	[property: Description("The user's score, out of 10; null or 0 if not scored.")]
	int? Score,
	DateOnly? StartDate,
	DateOnly? FinishDate)
{
	public static ListEntry? From(Tracking? tracking) => tracking?.Status is null ? null : new(
		tracking.Status,
		tracking.WatchedEpisodes,
		tracking.Score,
		tracking.StartDate is { } start ? DateOnly.FromDateTime(start) : null,
		tracking.FinishDate is { } finish ? DateOnly.FromDateTime(finish) : null);
}

[Description("An anime with everything known about it.")]
public sealed partial record AnimeDetails(
	AnimeSummary Anime,
	[property: Description("The synopsis, as plain text.")]
	string Description,
	IReadOnlyCollection<string> Studios,
	[property: Description("Its page on the anime database's website.")]
	string? Url,
	[property: Description("Related anime: sequels, prequels, side stories...")]
	IReadOnlyList<AnimeSummary> Related,
	[property: Description("Its trailers and promotional videos (YouTube links).")]
	IReadOnlyList<TrailerLink> Trailers)
{
	public static AnimeDetails From(AnimeModel anime) => new(
		AnimeSummary.From(anime),
		ToPlainText(anime.Description),
		anime.Studios,
		string.IsNullOrEmpty(anime.Url) ? null : anime.Url,
		[.. anime.Related.Select(AnimeSummary.From)],
		[.. anime.Trailers.Where(x => !string.IsNullOrEmpty(x.Url)).Select(x => new TrailerLink(string.IsNullOrWhiteSpace(x.Title) ? "Trailer" : x.Title, x.Url))]);

	/// <summary>The description's HTML (AniList's markup) as plain text.</summary>
	private static string ToPlainText(string html)
	{
		var text = LineBreak().Replace(html, "\n");
		text = Tag().Replace(text, "");
		return WebUtility.HtmlDecode(text).Trim();
	}

	[GeneratedRegex(@"<br\s*/?>|</p>", RegexOptions.IgnoreCase)]
	private static partial Regex LineBreak();

	[GeneratedRegex("<[^>]+>")]
	private static partial Regex Tag();
}

[Description("A trailer or promotional video of an anime.")]
public sealed record TrailerLink(string Title, string Url);

[Description("An episode of an anime.")]
public sealed record EpisodeSummary(
	[property: Description("The episode's number; specials are numbered separately.")]
	int Number,
	string Title,
	bool IsSpecial,
	DateOnly? Aired,
	int? RuntimeMinutes,
	[property: Description("Whether the user has watched it, going by their list entry.")]
	bool Watched,
	[property: Description("Where playing it resumes from (h:mm:ss), if the user stopped partway through.")]
	string? ResumeFrom)
{
	public static EpisodeSummary From(EpisodeInfo episode, int watched, EpisodeProgress? progress)
	{
		var title = episode.Titles.English is { Length: > 0 } english ? english
			: episode.Titles.Romaji is { Length: > 0 } romaji ? romaji
			: $"Episode {episode.EpisodeNumber}";
		return new EpisodeSummary(
			episode.EpisodeNumber,
			title,
			episode.IsSpecial,
			episode.AirDateUtc is { } aired ? DateOnly.FromDateTime(aired.LocalDateTime) : null,
			episode.Runtime > 0 ? episode.Runtime : null,
			!episode.IsSpecial && episode.EpisodeNumber <= watched,
			progress is null ? null : progress.Position.ToString(@"h\:mm\:ss"));
	}
}
