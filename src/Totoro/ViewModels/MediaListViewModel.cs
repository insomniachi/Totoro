using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Totoro.Core.Filtering;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Navigation;
using Totoro.Core.Settings;
using Totoro.UI.Navigation;
using Totoro.ViewModels.Manga;

namespace Totoro.ViewModels;

/// <summary>
/// The user's list, anime or manga, a status at a time, with filters and sorting: what both Library pages show (see
/// MediaListView). Subclasses load the list and follow changes to it.
/// </summary>
public abstract partial class MediaListViewModel : NavigatableViewModel
{
	private readonly IPreferences _preferences;
	private readonly string _sortKey;
	private readonly SourceCache<IMediaModel, long> _source = new(x => x.Id);
	private readonly ReadOnlyObservableCollection<IMediaModel> _items;

	// Position of each item in the list the tracker returned, so filtering keeps that order.
	private readonly Dictionary<long, int> _order = [];

	// The database the list is from, once loaded.
	private Guid _trackerId;

	/// <param name="signedOutAccount">The account the user has to sign in to for a list; null while they needn't.</param>
	protected MediaListViewModel(MediaMode kind, IPreferences preferences, Func<IAccount?> signedOutAccount)
	{
		Kind = kind;
		_preferences = preferences;
		// The anime list's keys are as they were before there were manga.
		_sortKey = kind == MediaMode.Manga ? "MangaListSort" : "UserListSort";
		StatusFilter = new ListStatusFilter(kind);
		SeasonFilter = kind == MediaMode.Anime ? new SeasonFilter() : null;
		SortOptions =
		[
			new("Last updated", ListSort.LastUpdated),
			new("Title", ListSort.Title),
			new("Your score", ListSort.UserScore),
			new("Average score", ListSort.AverageScore),
			.. kind == MediaMode.Anime ? [new FilterOption<ListSort>("Airing date", ListSort.Year)] : Array.Empty<FilterOption<ListSort>>(),
		];

		// Busy while it loads, retries too, for the placeholder cards.
		LoadState = new ListLoadState(signedOutAccount, () => RunBusyAsync(LoadListAsync), $"to see your {Noun} list");
		// The sort last picked; both are read first, as setting Sort resets (and saves) the direction.
		var sort = preferences.ReadKey(_sortKey, ListSort.LastUpdated);
		var descending = preferences.ReadKey(_sortKey + "Descending", true);
		Sort = SortOptions.FirstOrDefault(x => x.Value == sort) ?? SortOptions[0];
		SortDescending = descending;
		foreach (var filter in new INotifyPropertyChanged?[] { TitleFilter, GenreFilter, SeasonFilter }.OfType<INotifyPropertyChanged>())
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
			   // Only when its list entry changes (as "Tracking"): cards change other properties as they show (e.g. what the
			   // provider has), and refreshing on those would re-sort the list as they do, again and again.
			   .AutoRefreshOnObservable(x => Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(h => x.PropertyChanged += h, h => x.PropertyChanged -= h)
													.Where(e => e.EventArgs.PropertyName == "Tracking"))
			   .Filter(Filters.Combine())
			   .SortAndBind(out _items, _comparer, new SortAndBindOptions { ResetThreshold = int.MaxValue })
			   .Subscribe();

		// What the page says when it has no cards follows the cards, the tab, the filters, and whether loading went wrong.
		((INotifyCollectionChanged)_items).CollectionChanged += (_, _) => UpdateEmpty();
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

	/// <summary>Anime or manga.</summary>
	public MediaMode Kind { get; }

	public bool IsAnime => Kind == MediaMode.Anime;

	private string Noun => Kind == MediaMode.Manga ? "manga" : "anime";

	/// <summary>The user's list, from the selected database, and its database's id; null when there's no database.</summary>
	protected abstract Task<(Guid TrackerId, IReadOnlyList<IMediaModel> Items)?> GetListAsync();

	/// <summary>Why the page shows no cards, and what to do about it; null while it shows some (or the InfoBars say why).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsEmpty))]
	public partial ListEmptyState? Empty { get; private set; }

	public bool IsEmpty => Empty is not null;

	private void UpdateEmpty()
	{
		// Null while the tabs load (no tab selected yet).
		var tab = StatusFilter.Options.ElementAtOrDefault(StatusFilter.SelectedIndex);
		var manga = Kind == MediaMode.Manga;
		Empty = _items.Count > 0 || LoadState.HasMessage ? null
			: ListCount == 0 ? new("", "Your list is empty",
								   manga ? "Find something to read on Discover, or search for a manga, and add it to your list."
										 : "Find something to watch on Discover, or search for an anime, and add it to your list.", OffersDiscover: true)
			: HasActiveFilters ? new("", "No matches", $"Nothing in {tab?.Label ?? "your list"} matches your filters.", OffersClearFilters: true)
			: tab?.Value switch
			{
				null => null,
				ListItemStatus.Watching when manga => new("", "Not reading anything", "Read a manga, or set one to Reading, and it shows up here."),
				ListItemStatus.Watching => new("", "Not watching anything", "Play an anime, or set one to Watching, and it shows up here."),
				ListItemStatus.PlanToWatch => new("", "Nothing planned", $"Add {Noun} you want to {(manga ? "read" : "watch")} later, and they wait here."),
				ListItemStatus.Completed => new("", "Nothing completed yet", $"{(manga ? "Manga" : "Anime")} you finish end up here."),
				ListItemStatus.OnHold => new("", "Nothing on hold", $"{(manga ? "Manga" : "Anime")} you put on hold wait here until you pick them back up."),
				_ => new("", "Nothing dropped", $"{(manga ? "Manga" : "Anime")} you stop {(manga ? "reading" : "watching")} end up here."),
			};
	}

	[RelayCommand]
	private Task GoToDiscoverAsync() =>
		Navigators.TryGetNavigator("main", out var main)
			? main.NavigateToViewModelAsync(Kind == MediaMode.Manga ? typeof(MangaDiscoverViewModel) : typeof(DiscoverViewModel))
			: Task.CompletedTask;

	public ListStatusFilter StatusFilter { get; }

	public TitleFilter TitleFilter { get; } = new();

	public GenreFilter GenreFilter { get; } = new();

	/// <summary>The anime list's seasons and years; null on the manga list, which hasn't them.</summary>
	public SeasonFilter? SeasonFilter { get; }

	/// <summary>Whether the filters and sorting show (the Filters button is on).</summary>
	[ObservableProperty]
	public partial bool ShowFilters { get; set; }

	/// <summary>Whether any filter besides the status tabs narrows the list (so it can be cleared).</summary>
	public bool HasActiveFilters => TitleFilter.IsActive || GenreFilter.IsActive || SeasonFilter?.IsActive == true;

	/// <summary>Filtered view of the user's list, for the view to bind to.</summary>
	public ReadOnlyObservableCollection<IMediaModel> Items => _items;

	/// <summary>Every filter applied to the list; an item has to pass all of them. Add new filters here.</summary>
	private IEnumerable<IFilter<IMediaModel>> Filters => new IFilter<IMediaModel>?[] { StatusFilter, TitleFilter, GenreFilter, SeasonFilter }.OfType<IFilter<IMediaModel>>();

	public IReadOnlyList<FilterOption<ListSort>> SortOptions { get; }

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
		_preferences.SaveKey(_sortKey, value?.Value ?? ListSort.LastUpdated);
		UpdateSort();
	}

	partial void OnSortDescendingChanged(bool value)
	{
		_preferences.SaveKey(_sortKey + "Descending", value);
		UpdateSort();
	}

	[RelayCommand]
	private void ToggleSortDirection() => SortDescending = !SortDescending;

	[RelayCommand]
	private void ClearFilters()
	{
		TitleFilter.Text = "";
		GenreFilter.Clear();
		SeasonFilter?.Clear();
	}

	// Ties (and items without the value sorted by) keep the order the tracker returned.
	private readonly BehaviorSubject<IComparer<IMediaModel>> _comparer = new(Comparer<IMediaModel>.Default);

	private void UpdateSort()
	{
		var listOrder = SortExpressionComparer<IMediaModel>.Ascending(x => _order.GetValueOrDefault(x.Id, int.MaxValue));
		_comparer.OnNext(UserListQuery.Comparer(Sort?.Value ?? ListSort.LastUpdated, SortDescending, listOrder));
	}

	/// <summary>How many items are on the list, whatever the tab and filters; to tell an empty list from an empty tab.</summary>
	[ObservableProperty]
	public partial int ListCount { get; private set; }

	/// <summary>Whether the user is signed in, and whether the list loaded; for the page's InfoBars.</summary>
	public ListLoadState LoadState { get; }

	protected override Task OnNavigatedToInternalAsync() => LoadState.LoadAsync();

	private async Task LoadListAsync()
	{
		(Guid TrackerId, IReadOnlyList<IMediaModel> Items)? list;
		try
		{
			list = await GetListAsync();
		}
		catch
		{
			// The cards shown may be another list's (e.g. from before signing out); the error says why there are none.
			_source.Clear();
			ListCount = 0;
			throw;
		}

		if (list is not { } loaded)
		{
			return;
		}

		_trackerId = loaded.TrackerId;
		_order.Clear();
		foreach (var item in loaded.Items)
		{
			_order.TryAdd(item.Id, _order.Count);
		}

		// The genres and years to pick from are those of the list.
		GenreFilter.SetGenres(loaded.Items);
		UpdateSort();
		_source.Edit(cache =>
		{
			cache.Clear();
			cache.AddOrUpdate(loaded.Items);
		});
		ListCount = _source.Count;
		// An empty list changes nothing above (no cards before or after), so nothing else would say so.
		UpdateEmpty();
	}

	/// <summary>
	/// An item's list entry changed (in a dialog, by playing or reading it, or by an agent): moves its card to its new tab,
	/// or adds or removes it. Only for items from this list's database.
	/// </summary>
	/// <param name="update">Gives the listed item, a different instance (e.g. fetched by an agent tool), the new entry.</param>
	protected void OnListChanged<T>(T item, bool removed, Action<T> update) where T : IMediaModel
	{
		if (item.ServiceId is { } serviceId && serviceId != _trackerId)
		{
			return;
		}

		var listed = _source.Lookup(item.Id);
		if (removed)
		{
			_source.RemoveKey(item.Id);
		}
		else if (listed.HasValue && listed.Value is T same)
		{
			update(same);
		}
		else
		{
			_source.AddOrUpdate(item);
		}

		ListCount = _source.Count;
	}
}

/// <summary>What the list page says when it has no cards: a Segoe Fluent icon, a title, a hint, and what it offers to do.</summary>
public sealed record ListEmptyState(string Glyph, string Title, string Text, bool OffersDiscover = false, bool OffersClearFilters = false);
