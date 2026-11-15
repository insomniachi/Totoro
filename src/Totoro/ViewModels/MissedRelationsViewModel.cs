using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Filtering;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Relations;
using Totoro.Core.Services;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>
/// Discover's See all for its missed row: every sequel, prequel, side story and spin-off of what the user watched that
/// isn't on their list, in a grid, less the kinds and formats ticked off and sorted as the Library sorts.
/// </summary>
public sealed partial class MissedRelationsViewModel(MissedRelationsService missedRelations, ILogger<MissedRelationsViewModel> logger)
	: NavigatableViewModel, IRecipient<AnimeTrackingChangedMessage>, Core.Navigation.IAnimeDatabaseView
{
	private IReadOnlyList<MissedRelationGroup> _groups = [];
	private Dictionary<long, MissedRelationGroup> _groupOf = [];

	[ObservableProperty] public partial IReadOnlyList<MissedRelationGroup> Shown { get; private set; } = [];

	/// <summary>The kinds left out, ticked off on the page; <see cref="Refresh"/> after changing them.</summary>
	public HashSet<AnimeRelationType> HiddenRelations { get; } = [];

	/// <summary>The formats left out; music videos at first, as a sequel locator does. One of unknown format always shows.</summary>
	public HashSet<AnimeMediaFormat> HiddenFormats { get; } = [AnimeMediaFormat.Music];

	/// <summary>e.g. "42 missed"; or why there are none.</summary>
	[ObservableProperty] public partial string Summary { get; private set; } = "";

	public IReadOnlyList<FilterOption<ListSort>> SortOptions { get; } =
	[
		new("Popularity", ListSort.Popularity),
		new("Average score", ListSort.AverageScore),
		new("Airing date", ListSort.Year),
		new("Title", ListSort.Title),
	];

	[ObservableProperty] public partial FilterOption<ListSort>? Sort { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SortDirectionGlyph), nameof(SortDirectionName))]
	public partial bool SortDescending { get; set; } = true;

	// As the Library's sort button.
	public string SortDirectionGlyph => SortDescending ? "" : "";

	public string SortDirectionName => SortDescending ? "Descending" : "Ascending";

	protected override async Task OnNavigatedToInternalAsync()
	{
		WeakReferenceMessenger.Default.Register(this);
		// By title, A to Z (OnSortChanged makes a title sort ascending).
		Sort ??= SortOptions.First(x => x.Value == ListSort.Title);
		_finding = true;
		Show();
		try
		{
			// Progress reports on this (the UI) thread; one posted after the load finished is ignored.
			SetGroups(await missedRelations.GetGroupsAsync(new Progress<IReadOnlyList<MissedRelationGroup>>(found =>
			{
				if (_finding)
				{
					SetGroups(found);
					Show();
				}
			})));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't load missed sequels and spin-offs");
			Summary = "Couldn't load your list or its related anime. Try again later.";
			return;
		}
		finally
		{
			_finding = false;
		}

		Show();
	}

	// Still finding more: what's found so far shows, with placeholders only until there's some.
	private bool _finding;

	/// <summary>Placeholder franchises, while nothing's been found yet.</summary>
	[ObservableProperty] public partial bool ShowPlaceholders { get; private set; }

	private void SetGroups(IReadOnlyList<MissedRelationGroup> groups)
	{
		_groups = groups;
		_groupOf = groups.SelectMany(g => g.Missed.Select(x => (x.Id, g))).ToDictionary();
	}

	public void Refresh() => Show();

	partial void OnSortChanged(FilterOption<ListSort>? value)
	{
		// A title reads A to Z; the rest, highest (or latest) first.
		SortDescending = value?.Value != ListSort.Title;
		Show();
	}

	partial void OnSortDescendingChanged(bool value) => Show();

	[RelayCommand]
	private void ToggleSortDirection() => SortDescending = !SortDescending;

	/// <summary>Every franchise open, or every one closed (at first); one opened or closed by hand stays until the next sort, filter or update.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ExpandAllText))]
	public partial bool AllExpanded { get; set; }

	public string ExpandAllText => AllExpanded ? "Collapse all" : "Expand all";

	partial void OnAllExpandedChanged(bool value) => Show();

	[RelayCommand]
	private void ToggleExpandAll() => AllExpanded = !AllExpanded;

	/// <summary>
	/// Any title of the missed anime to show, or of a watched one behind its franchise (which shows all of the
	/// franchise's); empty for every one.
	/// </summary>
	[ObservableProperty] public partial string Query { get; set; } = "";

	partial void OnQueryChanged(string value) => Show();

	private void Show()
	{
		var searching = !string.IsNullOrWhiteSpace(Query);
		var filtered = _groups.SelectMany(g => g.Missed).Where(x => x.Relation is not { } relation || !HiddenRelations.Contains(relation))
							 .Where(x => !HiddenFormats.Contains(x.MediaFormat))
							 .Where(x => !searching || UserListQuery.MatchesTitle(x, Query) || _groupOf[x.Id].Watched.Any(w => UserListQuery.MatchesTitle(w, Query)));
		// A franchise goes where its first anime sorts, with its anime in that order. What's searched for shows open.
		var sorted = UserListQuery.Sort(filtered, Sort?.Value ?? ListSort.Popularity, SortDescending);
		Shown = [.. sorted.GroupBy(x => _groupOf[x.Id]).Select(g => g.Key with { Missed = [.. g], IsExpanded = AllExpanded || searching })];
		ShowPlaceholders = _finding && _groups.Count == 0;
		Summary = _finding ? $"{sorted.Count} missed in {Shown.Count} franchises so far, still looking…"
			: _groups.Count == 0 ? "Nothing missed: you've seen every sequel, prequel, side story and spin-off of what you watched, or there's no list to check."
			: $"{sorted.Count} missed in {Shown.Count} franchises";
	}

	protected override Task OnNavigatedFromInternalAsync()
	{
		WeakReferenceMessenger.Default.Unregister<AnimeTrackingChangedMessage>(this);
		return Task.CompletedTask;
	}

	/// <summary>One added from its card shows it's on the list now; it stays until the page is opened again.</summary>
	public void Receive(AnimeTrackingChangedMessage message)
	{
		foreach (var anime in _groups.SelectMany(g => g.Missed).Where(x => !ReferenceEquals(x, message.Anime) && x.StoreKey == message.Anime.StoreKey))
		{
			anime.Tracking = message.Tracking;
		}
	}
}
