using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Totoro.Core.Settings;
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
/// Searches an anime database by title, season, years, score and genres. Searches run by themselves as the filters
/// change (after a short pause), against the chosen provider; results are sorted by <see cref="SortField"/>.
/// </summary>
public partial class AdvancedSearchViewModel : NavigatableViewModel, IRecipient<AnimeTrackingChangedMessage>
{
	// How long to wait after the last change before searching: longer for typing, which changes more often.
	private static readonly TimeSpan s_filterDelay = TimeSpan.FromMilliseconds(500);
	private static readonly TimeSpan s_titleDelay = TimeSpan.FromSeconds(1);

	private readonly IFactory<IAnimeMetadataProvider, Guid> _providers;
	private readonly IFactory<IAnimeTracker, Guid> _trackers;
	private readonly ILogger<AdvancedSearchViewModel> _logger;
	private readonly SourceCache<AnimeModel, long> _results = new(x => x.Id);
	private readonly ReadOnlyObservableCollection<AnimeModel> _anime;
	private readonly CompositeDisposable _subscriptions = [];
	private IAnimeMetadataProvider? _provider;

	public AdvancedSearchViewModel(
		IFactory<IAnimeMetadataProvider, Guid> providers,
		IFactory<IAnimeTracker, Guid> trackers,
		IPreferences preferences,
		IEnumerable<Descriptor> modules,
		ILogger<AdvancedSearchViewModel> logger)
	{
		_providers = providers;
		_trackers = trackers;
		_logger = logger;

		// Results arrive on background threads; bindings need them on the UI thread, which creates this view model.
		var ui = new SynchronizationContextScheduler(SynchronizationContext.Current
													 ?? throw new InvalidOperationException("Create the view model on the UI thread."));

		_subscriptions.Add(_results.Connect()
								   .SortAndBind(out _anime, this.WhenAnyPropertyChanged(nameof(SortField), nameof(IsAscending))
																.Select(_ => CreateComparer())
																.StartWith(CreateComparer()))
								   .Subscribe());

		_subscriptions.Add(SearchWhenFiltersChange(ui));
		WeakReferenceMessenger.Default.Register(this);

		Providers = [.. providers.CreateAll().Select(x => new AnimeDatabaseOption(x.Id, x.Name, modules.FirstOrDefault(m => m.Id == x.Id)?.Image ?? ""))];
		// The selected anime database, as elsewhere in the app; unset (or unknown) means the first.
		Provider = Providers.FirstOrDefault(x => x.Id == preferences.SelectedAnimeDatabase) ?? Providers.FirstOrDefault();
		SelectedSeason = Seasons[0];
		SelectedSort = SortOptions.First(x => x.Value == SortField);
	}

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

	/// <summary>The genres the selected provider knows, to pick from.</summary>
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
	public ReadOnlyObservableCollection<AnimeModel> Anime => _anime;

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
		WeakReferenceMessenger.Default.Unregister<AnimeTrackingChangedMessage>(this);
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
		_provider = value is null ? null : _providers.Create(value.Id);
		AllGenres = [];

		if (_provider is not { } provider)
		{
			return;
		}

		try
		{
			var genres = await provider.GetGenresAsync();
			// Another provider may have been picked while these loaded.
			if (ReferenceEquals(provider, _provider))
			{
				AllGenres = genres;
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Couldn't load genres from {Provider}", provider.Name);
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

	private IObservable<IReadOnlyList<AnimeModel>> Search(AdvancedSearchRequest request)
	{
		_lastSearchWasEmpty = request.IsEmpty();
		if (_lastSearchWasEmpty || _provider is not { } provider)
		{
			// Nothing to search for: show nothing rather than the last results.
			return Observable.Return<IReadOnlyList<AnimeModel>>([]);
		}

		IsLoading = true;
		// Cancelled when another search starts (Switch), e.g. while retrying a misspelt title.
		return Observable.FromAsync(async ct =>
		{
			try
			{
				return (IReadOnlyList<AnimeModel>)await provider.FindAnimeAsync(request, _trackers.CreateDefault(), ct);
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

	/// <summary>An anime's list entry changed through another instance of it (e.g. by an agent tool): its card shows it.</summary>
	public void Receive(AnimeTrackingChangedMessage message)
	{
		foreach (var anime in _results.Items.Where(x => !ReferenceEquals(x, message.Anime) && x.StoreKey == message.Anime.StoreKey))
		{
			anime.Tracking = message.Tracking;
		}
	}

	private void ShowResults(IReadOnlyList<AnimeModel> anime)
	{
		_results.Edit(cache =>
		{
			cache.Clear();
			cache.AddOrUpdate(anime);
		});
		IsLoading = false;
		Message = anime.Count > 0 ? "" : _lastSearchWasEmpty ? NoFiltersMessage : "Nothing matches these filters.";
	}

	private AdvancedSearchRequest CreateRequest()
	{
		return new AdvancedSearchRequest
		{
			Title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim(),
			SeasonName = Season,
			MinYear = MinimumYear,
			MaxYear = MaximumYear,
			MinimumScore = MinimumScore,
			MaximumScore = MaximumScore,
			IncludedGenres = IncludedGenres.Count > 0 ? [.. IncludedGenres] : null,
			ExcludedGenres = ExcludedGenres.Count > 0 ? [.. ExcludedGenres] : null,
		};
	}

	private IComparer<AnimeModel> CreateComparer()
	{
		Func<AnimeModel, IComparable> key = SortField switch
		{
			SearchSortField.Popularity => x => x.Popularity,
			SearchSortField.Title => x => x.Title,
			_ => x => x.MeanScore ?? 0,
		};

		return IsAscending ? SortExpressionComparer<AnimeModel>.Ascending(key) : SortExpressionComparer<AnimeModel>.Descending(key);
	}

	private static IObservable<Unit> CollectionChanged(INotifyCollectionChanged collection)
	{
		return Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
							 h => collection.CollectionChanged += h,
							 h => collection.CollectionChanged -= h)
						 .Select(_ => Unit.Default);
	}
}
