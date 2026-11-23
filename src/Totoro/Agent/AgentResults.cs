using System.ComponentModel;
using System.Net;
using System.Text.RegularExpressions;
using Totoro.Core.Agent;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Services;

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
	ListEntry? ListEntry) : IAgentAnime
{
	// The anime it describes, for the chat to show (not part of its JSON: explicit interface members aren't serialized).
	private AnimeModel? _source;

	AnimeModel? IAgentAnime.Source => _source;

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
		ListEntry.From(anime.Tracking))
	{
		_source = anime,
	};
}

[Description("The user's list entry for an anime.")]
public sealed record ListEntry(
	ListItemStatus? Status,
	int? WatchedEpisodes,
	[property: Description("The user's score, out of 10; null or 0 if not scored.")]
	double? Score,
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
	IReadOnlyList<TrailerLink> Trailers) : IAgentAnime
{
	AnimeModel? IAgentAnime.Source => ((IAgentAnime)Anime).Source;

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
	[property: Description("Whether it's filler: not from the source material.")]
	bool IsFiller,
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
			episode.IsFiller,
			episode.AirDateUtc is { } aired ? DateOnly.FromDateTime(aired.LocalDateTime) : null,
			episode.Runtime > 0 ? episode.Runtime : null,
			!episode.IsSpecial && episode.EpisodeNumber <= watched,
			progress is null ? null : progress.Position.ToString(@"h\:mm\:ss"));
	}
}

[Description("An anime's songs: its openings, then its endings, then its insert songs.")]
public sealed record AnimeSongList(IReadOnlyList<AnimeSong> Songs) : IAgentSongs
{
	// The anime, for the chat to show the songs with their player.
	private AnimeModel? _anime;

	IReadOnlyList<SongMatch> IAgentSongs.Shown => _anime is null ? [] : [.. Songs.Select(x => new SongMatch(x, _anime))];

	public static AnimeSongList From(AnimeModel anime, IReadOnlyList<AnimeSong> songs) => new(songs) { _anime = anime };
}

[Description("Songs found by their title or singer, each with the anime it's from.")]
public sealed record FoundSongs(IReadOnlyList<FoundSong> Songs) : IAgentSongs
{
	private IReadOnlyList<SongMatch> _shown = [];

	IReadOnlyList<SongMatch> IAgentSongs.Shown => _shown;

	public static FoundSongs From(IReadOnlyList<(SongMatch Match, long AnimeId)> found) => new(
		[.. found.Select(x => new FoundSong(x.AnimeId, x.Match.Anime.Title, x.Match.Song.Kind, x.Match.Song.Number, x.Match.Song.Title, x.Match.Song.Artist))])
	{
		_shown = [.. found.Select(x => x.Match)],
	};
}

[Description("A song found by its title or singer, and the anime it's from; play_anime_song plays it with its animeId, kind and number.")]
public sealed record FoundSong(
	[property: Description("The anime's id in the user's anime database.")]
	long AnimeId,
	string AnimeTitle,
	AnimeSongKind Kind,
	[property: Description("Its number among its kind; null if it's the only one.")]
	int? Number,
	string Title,
	string Artist);

[Description("A song that started playing.")]
public sealed record SongPlaying(
	[property: Description("e.g. \"OP2: Bling-Bang-Bang-Born\".")]
	string Song,
	[property: Description("In the app (its audio) or the media player (its video).")]
	string PlayingIn) : IAgentSongs
{
	private AnimeModel? _anime;
	private AnimeSong? _song;

	IReadOnlyList<SongMatch> IAgentSongs.Shown => _song is null || _anime is null ? [] : [new SongMatch(_song, _anime)];

	public static SongPlaying From(AnimeModel anime, AnimeSong song, bool video) =>
		new($"{song.Label}: {song.Title}", video ? "the media player" : "the app") { _anime = anime, _song = song };
}

[Description("Anime, in the order asked for.")]
public sealed record ListEntries(
	[property: Description("How many anime match; these are the first of them.")]
	int Total,
	IReadOnlyList<AnimeSummary> Anime) : IAgentAnimeList
{
	IReadOnlyList<IAgentAnime> IAgentAnimeList.Ranked => Anime;
}

[Description("An anime's characters, which the user sees as a grid of their pictures and names; or, when the title could be several anime, those the user is asked to pick from.")]
public sealed record AnimeCharacters(
	[property: Description("The anime's title; empty while the user is picking.")]
	string Anime,
	IReadOnlyList<CharacterSummary> Characters,
	[property: Description("The anime the title could be (with their ids), shown to the user as buttons to pick one: stop and say nothing. Each time the user asks, call this tool again: the buttons are only shown then.")]
	IReadOnlyList<string>? PickOne = null) : IAgentCharacters, IAgentChoices
{
	private IReadOnlyList<CharacterModel> _shown = [];
	private IReadOnlyList<AgentChoice> _choices = [];

	IReadOnlyList<CharacterModel> IAgentCharacters.Shown => _shown;

	string IAgentChoices.Question => "Which anime's characters?";

	IReadOnlyList<AgentChoice> IAgentChoices.Choices => _choices;

	/// <summary>The anime a title could be, for the user to pick; each pick asks for its characters by id.</summary>
	public static AnimeCharacters Pick(IReadOnlyList<AnimeModel> candidates) => new("", [], [.. candidates.Select(x => $"{x.Title} (anime id {x.Id})")])
	{
		_choices = [.. candidates.Select(x => new AgentChoice(x.Title, $"Show me the characters of {x.Title} (anime id {x.Id})", x))],
	};

	public static AnimeCharacters From(AnimeModel anime, IReadOnlyList<CharacterModel> characters) => new(
		anime.Title,
		[.. characters.Select(x => new CharacterSummary(x.Name, string.IsNullOrEmpty(x.NativeName) ? null : x.NativeName))])
	{
		_shown = characters,
	};
}

[Description("A character of an anime.")]
public sealed record CharacterSummary(
	string Name,
	[property: Description("Its name in Japanese, if known.")]
	string? NativeName);

[Description("The anime a character appears in, most popular first.")]
public sealed record CharacterAnime(
	[property: Description("The character found, e.g. \"Himmel\".")]
	string Character,
	[property: Description("About how many anime it appears in (counting at most its 50 most popular); these are the first of them.")]
	int Total,
	IReadOnlyList<AnimeSummary> Anime) : IAgentAnimeList
{
	IReadOnlyList<IAgentAnime> IAgentAnimeList.Ranked => Anime;
}

[Description("The user's list, counted.")]
public sealed record ListStats(
	[property: Description("How many anime (that match).")]
	int Anime,
	[property: Description("How many of their episodes the user has watched, in all.")]
	int EpisodesWatched,
	[property: Description("The user's average score, out of 10, of those they've scored.")]
	double? AverageScore,
	[property: Description("The groups, biggest first.")]
	IReadOnlyList<ListGroup> Groups)
{
	public static ListStats From(IReadOnlyCollection<AnimeModel> list, IReadOnlyList<ListGroup> groups) =>
		new(list.Count, ListGroup.EpisodesOf(list), ListGroup.AverageScoreOf(list), groups);
}

[Description("Some of the user's list, e.g. their romance anime.")]
public sealed record ListGroup(
	string Name,
	[property: Description("How many anime.")]
	int Anime,
	[property: Description("How many of their episodes the user has watched.")]
	int EpisodesWatched,
	[property: Description("The user's average score, out of 10, of those they've scored.")]
	double? AverageScore)
{
	public static ListGroup From(string name, IReadOnlyCollection<AnimeModel> anime) =>
		new(name, anime.Count, EpisodesOf(anime), AverageScoreOf(anime));

	internal static int EpisodesOf(IEnumerable<AnimeModel> anime) => anime.Sum(x => x.Tracking?.WatchedEpisodes ?? 0);

	internal static double? AverageScoreOf(IEnumerable<AnimeModel> anime)
	{
		var scores = anime.Select(x => x.Tracking?.Score ?? 0).Where(x => x > 0).ToList();
		return scores.Count == 0 ? null : Math.Round(scores.Average(), 1);
	}
}

[Description("The results of testing anime provider plugins (Search, GetEpisodes, GetServers).")]
public sealed record AnimePluginTestReport(
	[property: Description("The show title queried during testing.")]
	string ShowTitle,
	[property: Description("Total number of provider plugins tested.")]
	int TotalTested,
	[property: Description("Number of plugins that passed all stages.")]
	int PassedCount,
	[property: Description("Number of plugins that failed at one or more stages.")]
	int FailedCount,
	[property: Description("Detailed breakdown of results for each plugin.")]
	IReadOnlyList<AnimePluginProviderReport> Plugins);

[Description("Test results for a single anime provider plugin.")]
public sealed record AnimePluginProviderReport(
	string Name,
	[property: Description("Whether all three stages (Search, GetEpisodes, GetServers) succeeded.")]
	bool Passed,
	[property: Description("Result of the Search stage.")]
	string SearchResult,
	[property: Description("Result of the GetEpisodes stage.")]
	string EpisodesResult,
	[property: Description("Result of the GetServers stage.")]
	string ServersResult,
	[property: Description("Error message if the plugin failed, otherwise null.")]
	string? Error);


[Description("One of the user's playlists of anime songs.")]
public sealed record PlaylistSummary(
	string Name,
	[property: Description("Its songs in order, e.g. \"OP2: Bling-Bang-Bang-Born (Mashle)\".")]
	IReadOnlyList<string> Songs,
	[property: Description("Whether it's the one playing.")]
	bool IsPlaying);

[Description("What's playing from a playlist.")]
public sealed record PlaylistPlaying(
	string Playlist,
	[property: Description("e.g. \"OP2: Bling-Bang-Bang-Born (Mashle)\"; empty if nothing's playing.")]
	string Song,
	bool Shuffle,
	PlaylistLoop Loop);

[Description("A user's review of an anime.")]
public sealed record ReviewSummary(
	[property: Description("The database it's from, e.g. Anilist; AniList's when the anime's own database has none.")]
	string Source,
	string Author,
	[property: Description("A line summing the review up, or the whole of a short one.")]
	string Summary,
	[property: Description("The score the reviewer gives it, out of 10.")]
	double? Score,
	[property: Description("How readers took it, e.g. \"42 of 50 found it helpful\".")]
	string? Reception,
	[property: Description("The full review, to open in the browser.")]
	string? Url)
{
	public static ReviewSummary From(ReviewModel review) =>
		new(review.Source, review.Author, review.Summary, review.Score is > 0 and var score ? score / 10.0 : null, review.Reception, review.Url);
}
