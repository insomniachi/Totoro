using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Navigation;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

public enum SearchSortField
{
	MeanScore,
	Popularity,
	Title,
}

// Choices for the view's dropdowns. ToString is what screen readers announce for each item.

public sealed record SeasonOption(string Label, AnimeSeason? Value)
{
	public override string ToString() => Label;
}

public sealed record SortOption(string Label, SearchSortField Value)
{
	public override string ToString() => Label;
}

/// <summary>
/// Searches a database, anime or manga, by title, years, score and genres (and, for anime, season): what both Search
/// pages show (see MediaSearchView). Searches run by themselves as the filters change (after a short pause), against
/// the chosen database; results are sorted by <see cref="SortField"/>. Subclasses do the searching.
/// </summary>
public abstract partial class MediaSearchViewModel : NavigatableViewModel
{
	// How long to wait after the last change before searching: longer for typing, which changes more often.
	private static readonly TimeSpan s_filterDelay = TimeSpan.FromMilliseconds(500);
	private static readonly TimeSpan s_titleDelay = TimeSpan.FromSeconds(1);

	private readonly ILogger _logger;
	private readonly SourceCache<IMediaModel, long> _results = new(x => x.Id);
	private readonly ReadOnlyObservableCollection<IMediaModel> _items;
	private readonly CompositeDisposable _subscriptions = [];

	/// <param name="providers">The databases that can be searched.</param>
	/// <param name="selected">The one selected elsewhere in the app; unset (or unknown) means the first.</param>
	protected MediaSearchViewModel(MediaMode kind, IReadOnlyList<AnimeDatabaseOption> providers, Guid selected, ILogger logger)
	{
		Kind = kind;
		_logger = logger;

		// Results arrive on background threads; bindings need them on the UI thread, which creates this view model.
		var ui = new SynchronizationContextScheduler(SynchronizationContext.Current
													 ?? throw new InvalidOperationException("Create the view model on the UI thread."));

		_subscriptions.Add(_results.Connect()
								   .SortAndBind(out _items, this.WhenAnyPropertyChanged(nameof(SortField), nameof(IsAscending))
																.Select(_ => CreateComparer())
																.StartWith(CreateComparer()))
								   .Subscribe());

		_subscriptions.Add(SearchWhenFiltersChange(ui));

		Providers = providers;
		Provider = Providers.FirstOrDefault(x => x.Id == selected) ?? Providers.FirstOrDefault();
		SelectedSeason = Seasons[0];
		SelectedSort = SortOptions.First(x => x.Value == SortField);
	}

	/// <summary>Anime or manga.</summary>
	public MediaMode Kind { get; }

	/// <summary>Whether it's anime, which (unlike manga) have seasons.</summary>
	public bool IsAnime => Kind == MediaMode.Anime;

	/// <summary>The genres the database knows, to pick from; none if it can't search by genre.</summary>
	protected abstract Task<List<string>> GetGenresAsync(Guid provider);

	/// <summary>What matches the request on the database.</summary>
	protected abstract Task<IReadOnlyList<IMediaModel>> SearchAsync(Guid provider, AdvancedSearchRequest request, CancellationToken ct);

	/// <summary>The results as they are, e.g. to give one a list entry changed elsewhere.</summary>
	protected IEnumerable<IMediaModel> Results => _results.Items;

	/// <summary>The databases that can be searched.</summary>
	public IReadOnlyList<AnimeDatabaseOption> Providers { get; }

	/// <summary>The database to search. Changing it clears the filters and loads its genres.</summary>
	[ObservableProperty] public partial AnimeDatabaseOption? Provider { get; set; }

	[ObservableProperty] public partial string? Title { get; set; }
	[ObservableProperty] public partial AnimeSeason? Season { get; set; }
	[ObservableProperty] public partial int? MinimumYear { get; set; }
	[ObservableProperty] public partial int? MaximumYear { get; set; }

	/// <summary>
	/// Whether the years filter on one year (<see cref="Year"/>) or a range (<see cref="MinimumYear"/> to
	/// <see cref="MaximumYear"/>). Any clears the years; Year narrows a range to its start.
	/// </summary>
	[ObservableProperty] public partial YearFilterMode YearMode { get; set; }

	/// <summary><see cref="YearMode"/> as an index, for a segmented control.</summary>
	public int YearModeIndex
	{
		get => (int)YearMode;
		set => YearMode = (YearFilterMode)value;
	}

	/// <summary>The year in <see cref="YearFilterMode.Year"/> mode: both ends of the range, so it's one year.</summary>
	public int? Year
	{
		get => MinimumYear;
		set => MinimumYear = MaximumYear = value;
	}

	partial void OnYearModeChanged(YearFilterMode value)
	{
		OnPropertyChanged(nameof(YearModeIndex));
		switch (value)
		{
			case YearFilterMode.Any:
				MinimumYear = MaximumYear = null;
				break;
			case YearFilterMode.Year:
				Year = MinimumYear ?? MaximumYear;
				break;
		}
	}

	partial void OnMinimumYearChanged(int? value) => OnPropertyChanged(nameof(Year));

	/// <summary>On a 0-10 scale, inclusive.</summary>
	[ObservableProperty] public partial float? MinimumScore { get; set; }

	/// <summary>On a 0-10 scale, inclusive.</summary>
	[ObservableProperty] public partial float? MaximumScore { get; set; }

	/// <summary>Genres a result must have. Edit the collection; it's never replaced.</summary>
	public ObservableCollection<string> IncludedGenres { get; } = [];

	/// <summary>Genres a result must not have. Edit the collection; it's never replaced.</summary>
	public ObservableCollection<string> ExcludedGenres { get; } = [];

	/// <summary>The genres the selected database knows, to pick from.</summary>
	[ObservableProperty] public partial IReadOnlyList<string> AllGenres { get; private set; } = [];

	[ObservableProperty] public partial SearchSortField SortField { get; set; } = SearchSortField.MeanScore;
	[ObservableProperty] public partial bool IsAscending { get; set; }

	/// <summary>True while a search is running.</summary>
	[ObservableProperty] public partial bool IsLoading { get; private set; }

	/// <summary>
	/// Why there are no results (no filters yet, or nothing matched); empty, never null, when there are some: x:Bind
	/// doesn't call a function whose argument is null (e.g. <c>VisibleIfAny(Message)</c>), so a null would leave it shown.
	/// </summary>
	[ObservableProperty] public partial string Message { get; private set; } = NoFiltersMessage;

	private const string NoFiltersMessage = "Type a title or pick some filters to search.";
	private bool _lastSearchWasEmpty = true;

	/// <summary>The results, sorted.</summary>
	public ReadOnlyObservableCollection<IMediaModel> Items => _items;

	/// <summary>Seasons to pick from, for <see cref="SelectedSeason"/>; the first is "any season".</summary>
	public IReadOnlyList<SeasonOption> Seasons { get; } =
	[
		new("Any", null),
		new("Winter", AnimeSeason.Winter),
		new("Spring", AnimeSeason.Spring),
		new("Summer", AnimeSeason.Summer),
		new("Fall", AnimeSeason.Fall),
	];

	/// <summary><see cref="Season"/> as one of <see cref="Seasons"/>, for a dropdown; the two stay in step.</summary>
	[ObservableProperty] public partial SeasonOption? SelectedSeason { get; set; }

	partial void OnSelectedSeasonChanged(SeasonOption? value) => Season = value?.Value;

	partial void OnSeasonChanged(AnimeSeason? value) => SelectedSeason = Seasons.First(x => x.Value == value);

	public IReadOnlyList<SortOption> SortOptions { get; } =
	[
		new("Score", SearchSortField.MeanScore),
		new("Popularity", SearchSortField.Popularity),
		new("Title", SearchSortField.Title),
	];

	/// <summary><see cref="SortField"/> as one of <see cref="SortOptions"/>, for a dropdown; the two stay in step.</summary>
	[ObservableProperty] public partial SortOption? SelectedSort { get; set; }

	partial void OnSelectedSortChanged(SortOption? value)
	{
		if (value is not null)
		{
			SortField = value.Value;
		}
	}

	partial void OnSortFieldChanged(SearchSortField value)
	{
		SelectedSort = SortOptions.First(x => x.Value == value);
		OnPropertyChanged(nameof(SortOrderText));
	}

	partial void OnIsAscendingChanged(bool value) => OnPropertyChanged(nameof(SortOrderText));

	/// <summary>The sort order in words for the sort field, e.g. "Highest first" or "A to Z".</summary>
	public string SortOrderText => (SortField, IsAscending) switch
	{
		(SearchSortField.Title, true) => "A to Z",
		(SearchSortField.Title, false) => "Z to A",
		(_, true) => "Lowest first",
		(_, false) => "Highest first",
	};

	[RelayCommand]
	private void ToggleSortOrder() => IsAscending = !IsAscending;

	/// <summary>The score range in words, e.g. "7.5 to 10" or "Any score".</summary>
	public string ScoreRangeText => (MinimumScore, MaximumScore) switch
	{
		(null, null) => "Any score",
		({ } min, null) => $"{min:0.#} and up",
		(null, { } max) => $"Up to {max:0.#}",
		({ } min, { } max) => $"{min:0.#} to {max:0.#}",
	};

	partial void OnMinimumScoreChanged(float? value) => OnPropertyChanged(nameof(ScoreRangeText));

	partial void OnMaximumScoreChanged(float? value) => OnPropertyChanged(nameof(ScoreRangeText));

	public int CurrentYear { get; } = DateTime.Now.Year;

	protected override Task OnNavigatedFromInternalAsync()
	{
		_subscriptions.Dispose();
		return Task.CompletedTask;
	}

	[RelayCommand]
	private void ClearSearchFilters()
	{
		Title = "";
		Season = null;
		YearMode = YearFilterMode.Any;
		MinimumYear = null;
		MaximumYear = null;
		MinimumScore = null;
		MaximumScore = null;
		IncludedGenres.Clear();
		ExcludedGenres.Clear();
		// With no filters left, the search clears the results by itself; clearing them now just doesn't wait for it.
		_results.Clear();
		Message = NoFiltersMessage;
	}

	/// <summary>What the current season button does, e.g. "This season (Fall 2026)", for its tooltip and name.</summary>
	public string CurrentSeasonName
	{
		get
		{
			var season = Core.Modules.Anime.Season.Of(DateTime.Now);
			return $"This season ({season.SeasonName} {season.Year})";
		}
	}

	/// <summary>Filters to this season's anime.</summary>
	[RelayCommand]
	private void CurrentSeason()
	{
		var season = Core.Modules.Anime.Season.Of(DateTime.Now);
		YearMode = YearFilterMode.Year;
		Year = season.Year;
		Season = season.SeasonName;
	}

	async partial void OnProviderChanged(AnimeDatabaseOption? value)
	{
		ClearSearchFilters();
		AllGenres = [];

		if (value is null)
		{
			return;
		}

		try
		{
			var genres = await GetGenresAsync(value.Id);
			// Another database may have been picked while these loaded.
			if (ReferenceEquals(value, Provider))
			{
				AllGenres = genres;
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Couldn't load genres from {Provider}", value.Name);
		}
	}

	/// <summary>Searches whenever the filters change, after they've settled; a newer search replaces one still running.</summary>
	private IDisposable SearchWhenFiltersChange(IScheduler ui)
	{
		var filtersChanged = this.WhenAnyPropertyChanged(nameof(Season), nameof(MinimumYear), nameof(MaximumYear), nameof(MinimumScore), nameof(MaximumScore))
								 .Throttle(s_filterDelay)
								 .Select(_ => Unit.Default);
		var titleChanged = this.WhenValueChanged(x => x.Title, notifyOnInitialValue: false)
							   .Throttle(s_titleDelay)
							   .Select(_ => Unit.Default);
		var genresChanged = Observable.Merge(CollectionChanged(IncludedGenres), CollectionChanged(ExcludedGenres))
									  .Throttle(s_filterDelay);

		return Observable.Merge(filtersChanged, titleChanged, genresChanged)
						 // Read the filters and update IsLoading on the UI thread, where they're bound.
						 .ObserveOn(ui)
						 .Select(_ => Search(CreateRequest()))
						 .Switch()
						 .ObserveOn(ui)
						 .Subscribe(ShowResults);
	}

	private IObservable<IReadOnlyList<IMediaModel>> Search(AdvancedSearchRequest request)
	{
		_lastSearchWasEmpty = request.IsEmpty();
		if (_lastSearchWasEmpty || Provider is not { } provider)
		{
			// Nothing to search for: show nothing rather than the last results.
			return Observable.Return<IReadOnlyList<IMediaModel>>([]);
		}

		IsLoading = true;
		// Cancelled when another search starts (Switch), e.g. while retrying a misspelt title.
		return Observable.FromAsync(async ct =>
		{
			try
			{
				return await SearchAsync(provider.Id, request, ct);
			}
			catch (OperationCanceledException)
			{
				return [];
			}
			catch (Exception ex)
			{
				// Keep the subscription alive for the next search.
				_logger.LogWarning(ex, "Search failed on {Provider}", provider.Name);
				return [];
			}
		});
	}

	private void ShowResults(IReadOnlyList<IMediaModel> items)
	{
		_results.Edit(cache =>
		{
			cache.Clear();
			cache.AddOrUpdate(items);
		});
		IsLoading = false;
		Message = items.Count > 0 ? "" : _lastSearchWasEmpty ? NoFiltersMessage : "Nothing matches these filters.";
	}

	private AdvancedSearchRequest CreateRequest()
	{
		return new AdvancedSearchRequest
		{
			Title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim(),
			// Only anime have seasons; the box isn't shown for manga.
			SeasonName = IsAnime ? Season : null,
			MinYear = MinimumYear,
			MaxYear = MaximumYear,
			MinimumScore = MinimumScore,
			MaximumScore = MaximumScore,
			IncludedGenres = IncludedGenres.Count > 0 ? [.. IncludedGenres] : null,
			ExcludedGenres = ExcludedGenres.Count > 0 ? [.. ExcludedGenres] : null,
		};
	}

	private IComparer<IMediaModel> CreateComparer()
	{
		Func<IMediaModel, IComparable> key = SortField switch
		{
			SearchSortField.Popularity => x => x.Popularity,
			SearchSortField.Title => x => x.Title,
			_ => x => x.MeanScore ?? 0,
		};

		return IsAscending ? SortExpressionComparer<IMediaModel>.Ascending(key) : SortExpressionComparer<IMediaModel>.Descending(key);
	}

	private static IObservable<Unit> CollectionChanged(INotifyCollectionChanged collection)
	{
		return Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
							 h => collection.CollectionChanged += h,
							 h => collection.CollectionChanged -= h)
						 .Select(_ => Unit.Default);
	}
}
