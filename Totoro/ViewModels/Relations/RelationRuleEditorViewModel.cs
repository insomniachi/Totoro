using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Relations;

namespace Totoro.ViewModels.Relations;

/// <summary>An anime a rule is about: its title, to show, and its ids, which the rule keeps.</summary>
/// <param name="Episodes">Its number of episodes, if known.</param>
public sealed record RelationAnime(string Title, string Details, RelationIds Ids, int? Episodes = null)
{
	public static RelationAnime From(AnimeModel anime)
	{
		List<string> details = [];
		if (anime.Season is { Year: > 1900 } season)
		{
			details.Add($"{season.SeasonName} {season.Year}");
		}

		if (anime.TotalEpisodes is > 0 and var episodes)
		{
			details.Add($"{episodes} episodes");
		}

		return new RelationAnime(anime.Title, string.Join(" · ", details), RelationIds.Of(anime), anime.TotalEpisodes is > 0 ? anime.TotalEpisodes : null);
	}

	public override string ToString() => Title;
}

/// <summary>
/// Edits an anime-relations rule, worded as what it means for watching: "this season's episodes 1 to 13 are numbered
/// on from 13 by an earlier entry". Anime are picked by searching the selected anime database, which gives their ids.
/// Used by the rule dialog, and inline in an anime's settings, where the season is that anime.
/// </summary>
public sealed partial class RelationRuleEditorViewModel : ObservableObject
{
	private readonly IAnimeMetadataProvider? _database;
	private CancellationTokenSource? _search;

	/// <param name="season">The season the rule is for, if it's given (then it can't be changed).</param>
	public RelationRuleEditorViewModel(IAnimeMetadataProvider? database, AnimeModel? season = null)
	{
		_database = database;
		if (season is not null)
		{
			Season = RelationAnime.From(season);
			SeasonLast = season.TotalEpisodes is > 0 and var total ? total : double.NaN;
			CanChangeSeason = false;
		}
	}

	/// <summary>Edits an existing rule.</summary>
	public RelationRuleEditorViewModel(IAnimeMetadataProvider? database, AnimeRelationRule rule) : this(database)
	{
		// Rules only keep ids; their title names both anime, e.g. "Seihantai na Kimi to Boku -> ~ 2nd Season".
		var titles = (rule.Title ?? "").Split("->", 2, StringSplitOptions.TrimEntries);
		var earlier = titles.Length > 0 && titles[0].Length > 0 ? titles[0] : IdsText(rule.Source);
		var season = titles.Length > 1 && titles[1].Length > 0 ? titles[1].Replace("~", earlier) : IdsText(rule.Destination);

		Earlier = new RelationAnime(earlier, IdsText(rule.Source), rule.Source);
		Season = new RelationAnime(season, IdsText(rule.Destination), rule.Destination);
		SeasonFirst = rule.DestinationEpisodes.First;
		SeasonLast = rule.DestinationEpisodes.Last is { } last ? last : double.NaN;
		EarlierFirst = rule.SourceEpisodes.First;
		AlsoCountsOnItself = rule.AlsoCountsOnItself;
	}

	/// <summary>The season whose episodes the rule places, e.g. a 2nd season.</summary>
	[ObservableProperty] public partial RelationAnime? Season { get; set; }

	public bool CanChangeSeason { get; } = true;

	// Doubles: NumberBox works in doubles, NaN for empty.
	[ObservableProperty] public partial double SeasonFirst { get; set; } = 1;

	/// <summary>The season's last episode the rule covers; empty (NaN) while it's still airing.</summary>
	[ObservableProperty] public partial double SeasonLast { get; set; } = double.NaN;

	/// <summary>The entry whose numbering the provider continues, e.g. the first season.</summary>
	[ObservableProperty] public partial RelationAnime? Earlier { get; set; }

	/// <summary>The number the provider gives the season's first episode (of those covered), e.g. 13.</summary>
	[ObservableProperty] public partial double EarlierFirst { get; set; } = double.NaN;

	/// <summary>The rule's "!": also when the provider names the show as this season but still numbers it on.</summary>
	[ObservableProperty] public partial bool AlsoCountsOnItself { get; set; } = true;

	[ObservableProperty] public partial IReadOnlyList<RelationAnime> SeasonResults { get; private set; } = [];
	[ObservableProperty] public partial IReadOnlyList<RelationAnime> EarlierResults { get; private set; } = [];

	/// <summary>What the rule means, in words; or what's still missing. Never null (it's passed to x:Bind functions).</summary>
	public string Summary => ToRule() is { } rule
		? $"{Season!.Title}'s episodes {rule.DestinationEpisodes} are {Earlier!.Title}'s {rule.SourceEpisodes}."
		: Season is null ? "Choose the season."
		: Earlier is null ? "Choose the entry it's numbered on from."
		: "Give the number its provider gives the season's first episode.";

	public bool IsValid => ToRule() is not null;

	partial void OnSeasonChanged(RelationAnime? value)
	{
		// Usually the whole season. Whenever one's chosen: going through the suggestions with the arrow keys chooses each.
		if (value?.Episodes is { } episodes)
		{
			SeasonLast = episodes;
		}

		OnRuleChanged();
	}
	partial void OnSeasonFirstChanged(double value) => OnRuleChanged();
	partial void OnSeasonLastChanged(double value) => OnRuleChanged();
	partial void OnEarlierChanged(RelationAnime? value) => OnRuleChanged();
	partial void OnEarlierFirstChanged(double value) => OnRuleChanged();
	partial void OnAlsoCountsOnItselfChanged(bool value) => OnRuleChanged();

	/// <summary>The rule as edited; null until it's complete.</summary>
	public AnimeRelationRule? ToRule()
	{
		if (Season is null || Earlier is null || !IsEpisode(SeasonFirst) || !IsEpisode(EarlierFirst) ||
			IsEpisode(SeasonLast) && SeasonLast < SeasonFirst)
		{
			return null;
		}

		int? seasonLast = IsEpisode(SeasonLast) ? (int)SeasonLast : null;
		return new AnimeRelationRule(
			Earlier.Ids,
			new EpisodeRange((int)EarlierFirst, seasonLast is { } last ? (int)EarlierFirst + last - (int)SeasonFirst : null),
			Season.Ids,
			new EpisodeRange((int)SeasonFirst, seasonLast),
			AlsoCountsOnItself)
		{
			Title = $"{Earlier.Title} -> {Season.Title}",
		};
	}

	/// <summary>Searches the anime database for the season, as the user types.</summary>
	public Task SearchSeasonAsync(string query) => SearchAsync(query, results => SeasonResults = results);

	/// <summary>Searches the anime database for the earlier entry, as the user types.</summary>
	public Task SearchEarlierAsync(string query) => SearchAsync(query, results => EarlierResults = results);

	private async Task SearchAsync(string query, Action<IReadOnlyList<RelationAnime>> show)
	{
		_search?.Cancel();
		var search = _search = new CancellationTokenSource();
		if (_database is null || query.Trim().Length < 2)
		{
			show([]);
			return;
		}

		try
		{
			// Searches after typing pauses.
			await Task.Delay(300, search.Token);
			var anime = await _database.SearchAnimeAsync(query.Trim());
			if (!search.IsCancellationRequested)
			{
				show([.. anime.Take(10).Select(RelationAnime.From)]);
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception)
		{
			// A failed search just shows nothing.
			show([]);
		}
	}

	private void OnRuleChanged()
	{
		OnPropertyChanged(nameof(Summary));
		OnPropertyChanged(nameof(IsValid));
	}

	private static bool IsEpisode(double value) => !double.IsNaN(value) && value >= 1;

	private static string IdsText(RelationIds ids)
	{
		List<string> parts = [];
		if (ids.MyAnimeList is { } mal)
		{
			parts.Add($"MyAnimeList {mal}");
		}

		if (ids.AniList is { } anilist)
		{
			parts.Add($"AniList {anilist}");
		}

		return parts.Count > 0 ? string.Join(" · ", parts) : "Unknown anime";
	}
}
