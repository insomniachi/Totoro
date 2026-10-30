using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Filtering;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>
/// Discover's See all for its missed row: every sequel, prequel, side story and spin-off of what the user watched that
/// isn't on their list, in a grid, narrowed to some kinds by <see cref="Filter"/> and sorted as the Library sorts.
/// </summary>
public sealed partial class MissedRelationsViewModel(MissedRelationsService missedRelations, ILogger<MissedRelationsViewModel> logger)
	: NavigatableViewModel, IRecipient<AnimeTrackingChangedMessage>
{
	private IReadOnlyList<AnimeModel> _all = [];

	[ObservableProperty] public partial IReadOnlyList<AnimeModel> Shown { get; private set; } = [];

	/// <summary>The kinds shown; null for all of them.</summary>
	[ObservableProperty] public partial IReadOnlyCollection<AnimeRelationType>? Filter { get; set; }

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
		Sort ??= SortOptions[0];
		try
		{
			_all = await missedRelations.GetAsync();
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't load missed sequels and spin-offs");
			Summary = "Couldn't load your list or its related anime. Try again later.";
			return;
		}

		Show();
	}

	partial void OnFilterChanged(IReadOnlyCollection<AnimeRelationType>? value) => Show();

	partial void OnSortChanged(FilterOption<ListSort>? value)
	{
		// A title reads A to Z; the rest, highest (or latest) first.
		SortDescending = value?.Value != ListSort.Title;
		Show();
	}

	partial void OnSortDescendingChanged(bool value) => Show();

	[RelayCommand]
	private void ToggleSortDirection() => SortDescending = !SortDescending;

	private void Show()
	{
		var filtered = _all.Where(x => Filter is null || x.Relation is { } relation && Filter.Contains(relation));
		Shown = UserListQuery.Sort(filtered, Sort?.Value ?? ListSort.Popularity, SortDescending);
		Summary = _all.Count == 0 ? "Nothing missed: you've seen every sequel, prequel, side story and spin-off of what you watched, or there's no list to check."
			: $"{Shown.Count} missed";
	}

	protected override Task OnNavigatedFromInternalAsync()
	{
		WeakReferenceMessenger.Default.Unregister<AnimeTrackingChangedMessage>(this);
		return Task.CompletedTask;
	}

	/// <summary>One added from its card shows it's on the list now; it stays until the page is opened again.</summary>
	public void Receive(AnimeTrackingChangedMessage message)
	{
		foreach (var anime in _all.Where(x => !ReferenceEquals(x, message.Anime) && x.StoreKey == message.Anime.StoreKey))
		{
			anime.Tracking = message.Tracking;
		}
	}
}
