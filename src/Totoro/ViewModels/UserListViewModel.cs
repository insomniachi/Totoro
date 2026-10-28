using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;
using DynamicData.Binding;
using Totoro.Core.Filtering;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

public partial class UserListViewModel : NavigatableViewModel, IRecipient<AnimeTrackingChangedMessage>
{
	private readonly IFactory<IAnimeTracker, Guid> _factory;
	private readonly SourceCache<AnimeModel, long> _source = new(x => x.Id);
	private readonly ReadOnlyObservableCollection<AnimeModel> _anime;

	// Position of each item in the list the tracker returned, so filtering keeps that order.
	private readonly Dictionary<long, int> _order = [];

	// The database the list is from, once loaded.
	private Guid _trackerId;

	public UserListViewModel(IFactory<IAnimeTracker, Guid> factory, AnimeTrackingService tracking)
	{
		_factory = factory;
		// Busy while it loads, retries too, for the placeholder cards.
		LoadState = new ListLoadState(tracking, () => RunBusyAsync(LoadListAsync), "to see your anime list");
		Sort = SortOptions[0];
		foreach (var filter in new INotifyPropertyChanged[] { TitleFilter, GenreFilter, SeasonFilter })
		{
			filter.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == "IsActive")
				{
					OnPropertyChanged(nameof(HasActiveFilters));
				}
			};
		}

		// Changes are pushed on the UI thread (the list is loaded from OnNavigatedToAsync and the
		// filters change from bindings), so no ObserveOn is needed before binding.
		// ResetThreshold is maxed so the view always gets individual adds/removes, never a Reset:
		// the ItemsRepeater can only animate cards moving into place for individual changes.
		_source.Connect()
			   .AutoRefresh(x => x.Tracking)
			   .Filter(Filters.Combine())
			   .SortAndBind(out _anime, _comparer, new SortAndBindOptions { ResetThreshold = int.MaxValue })
			   .Subscribe();

		// What the page says when it has no cards follows the cards, the tab, the filters, and whether loading went wrong.
		((INotifyCollectionChanged)_anime).CollectionChanged += (_, _) => UpdateEmpty();
		StatusFilter.PropertyChanged += (_, _) => UpdateEmpty();
		LoadState.PropertyChanged += (_, _) => UpdateEmpty();
		PropertyChanged += (_, e) =>
		{
			if (e.PropertyName is nameof(ListCount) or nameof(HasActiveFilters))
			{
				UpdateEmpty();
			}
		};
	}

	/// <summary>Why the page shows no cards, and what to do about it; null while it shows some (or the InfoBars say why).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsEmpty))]
	public partial ListEmptyState? Empty { get; private set; }

	public bool IsEmpty => Empty is not null;

	private void UpdateEmpty()
	{
		// Null while the tabs load (no tab selected yet).
		var tab = StatusFilter.Options.ElementAtOrDefault(StatusFilter.SelectedIndex);
		Empty = _anime.Count > 0 || LoadState.HasMessage ? null
			: ListCount == 0 ? new("", "Your list is empty",
								   "Find something to watch on Discover, or search for an anime, and add it to your list.", OffersDiscover: true)
			: HasActiveFilters ? new("", "No matches", $"Nothing in {tab?.Label ?? "your list"} matches your filters.", OffersClearFilters: true)
			: tab?.Value switch
			{
				null => null,
				ListItemStatus.Watching => new("", "Not watching anything", "Play an anime, or set one to Watching, and it shows up here."),
				ListItemStatus.PlanToWatch => new("", "Nothing planned", "Add anime you want to watch later, and they wait here."),
				ListItemStatus.Completed => new("", "Nothing completed yet", "Anime you finish end up here."),
				ListItemStatus.OnHold => new("", "Nothing on hold", "Anime you put on hold wait here until you pick them back up."),
				_ => new("", "Nothing dropped", "Anime you stop watching end up here."),
			};
	}

	[RelayCommand]
	private static Task GoToDiscoverAsync() =>
		Navigators.TryGetNavigator("main", out var main) ? main.NavigateToViewModelAsync(typeof(DiscoverViewModel)) : Task.CompletedTask;

	public ListStatusFilter StatusFilter { get; } = new();

	public TitleFilter TitleFilter { get; } = new();

	public GenreFilter GenreFilter { get; } = new();

	public SeasonFilter SeasonFilter { get; } = new();

	/// <summary>Whether the filters and sorting show (the Filters button is on).</summary>
	[ObservableProperty]
	public partial bool ShowFilters { get; set; }

	/// <summary>Whether any filter besides the status tabs narrows the list (so it can be cleared).</summary>
	public bool HasActiveFilters => TitleFilter.IsActive || GenreFilter.IsActive || SeasonFilter.IsActive;

	/// <summary>Filtered view of the user's list, for the view to bind to.</summary>
	public ReadOnlyObservableCollection<AnimeModel> Anime => _anime;

	/// <summary>Every filter applied to the list; an item has to pass all of them. Add new filters here.</summary>
	private IEnumerable<IFilter<AnimeModel>> Filters => [StatusFilter, TitleFilter, GenreFilter, SeasonFilter];

	public IReadOnlyList<FilterOption<ListSort>> SortOptions { get; } =
	[
		new("Last updated", ListSort.LastUpdated),
		new("Title", ListSort.Title),
		new("Your score", ListSort.UserScore),
		new("Average score", ListSort.AverageScore),
		new("Airing date", ListSort.Year),
	];

	/// <summary>What the list is sorted by.</summary>
	[ObservableProperty]
	public partial FilterOption<ListSort>? Sort { get; set; }

	/// <summary>Highest (or latest, or Z) first; for a title, A to Z unless it's on.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(SortDirectionGlyph), nameof(SortDirectionName))]
	public partial bool SortDescending { get; set; } = true;

	public string SortDirectionGlyph => SortDescending ? "" : "";

	public string SortDirectionName => SortDescending ? "Descending" : "Ascending";

	partial void OnSortChanged(FilterOption<ListSort>? value)
	{
		// A title reads A to Z; the rest, highest (or latest) first.
		SortDescending = value?.Value != ListSort.Title;
		UpdateSort();
	}

	partial void OnSortDescendingChanged(bool value) => UpdateSort();

	[RelayCommand]
	private void ToggleSortDirection() => SortDescending = !SortDescending;

	[RelayCommand]
	private void ClearFilters()
	{
		TitleFilter.Text = "";
		GenreFilter.Clear();
		SeasonFilter.Clear();
	}

	// Ties (and anime without the value sorted by) keep the order the tracker returned.
	private readonly BehaviorSubject<IComparer<AnimeModel>> _comparer = new(Comparer<AnimeModel>.Default);

	private void UpdateSort()
	{
		var listOrder = SortExpressionComparer<AnimeModel>.Ascending(x => _order.GetValueOrDefault(x.Id, int.MaxValue));
		_comparer.OnNext(UserListQuery.Comparer(Sort?.Value ?? ListSort.LastUpdated, SortDescending, listOrder));
	}

	/// <summary>How many anime are on the list, whatever the tab and filters; to tell an empty list from an empty tab.</summary>
	[ObservableProperty]
	public partial int ListCount { get; private set; }

	/// <summary>Whether the user is signed in, and whether the list loaded; for the page's InfoBars.</summary>
	public ListLoadState LoadState { get; }

	protected override Task OnNavigatedToInternalAsync() => LoadState.LoadAsync();

	private async Task LoadListAsync()
	{
		var tracker = _factory.CreateDefault();

		if (tracker is null)
		{
			return;
		}

		_trackerId = tracker.Id;
		if (!WeakReferenceMessenger.Default.IsRegistered<AnimeTrackingChangedMessage>(this))
		{
			WeakReferenceMessenger.Default.Register(this);
		}

		List<AnimeModel> anime;
		try
		{
			anime = await tracker.GetUserListAsync(CancellationToken.None);
		}
		catch
		{
			// The cards shown may be another list's (e.g. from before signing out); the error says why there are none.
			_source.Clear();
			ListCount = 0;
			throw;
		}

		_order.Clear();
		foreach (var item in anime)
		{
			_order.TryAdd(item.Id, _order.Count);
		}

		// The genres and years to pick from are those of the list.
		GenreFilter.SetGenres(anime);
		UpdateSort();
		_source.Edit(cache =>
		{
			cache.Clear();
			cache.AddOrUpdate(anime);
		});
		ListCount = _source.Count;
		// An empty list changes nothing above (no cards before or after), so nothing else would say so.
		UpdateEmpty();
	}

	protected override Task OnNavigatedFromInternalAsync()
	{
		WeakReferenceMessenger.Default.Unregister<AnimeTrackingChangedMessage>(this);
		return Task.CompletedTask;
	}

	/// <summary>
	/// An anime's list entry changed (in a dialog, by playing it, or by an agent): moves its card to its new tab, or
	/// adds or removes it. Only for anime from this list's database.
	/// </summary>
	public void Receive(AnimeTrackingChangedMessage message)
	{
		var anime = message.Anime;
		if (anime.ServiceId is { } serviceId && serviceId != _trackerId)
		{
			return;
		}

		var listed = _source.Lookup(anime.Id);
		if (message.Tracking is null)
		{
			_source.RemoveKey(anime.Id);
		}
		else if (listed.HasValue)
		{
			// A different instance (e.g. fetched by an agent tool): the card shows the listed one.
			listed.Value.Tracking = message.Tracking;
		}
		else
		{
			_source.AddOrUpdate(anime);
		}

		ListCount = _source.Count;
	}
}

/// <summary>What the list page says when it has no cards: a Segoe Fluent icon, a title, a hint, and what it offers to do.</summary>
public sealed record ListEmptyState(string Glyph, string Title, string Text, bool OffersDiscover = false, bool OffersClearFilters = false);
