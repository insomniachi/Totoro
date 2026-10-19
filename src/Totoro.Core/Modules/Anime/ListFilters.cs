using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using Totoro.Core.Filtering;

namespace Totoro.Core.Modules.Anime;

/// <summary>Anime whose title (any of them) contains the text typed.</summary>
public sealed partial class TitleFilter : ObservableObject, IFilter<AnimeModel>
{
	public TitleFilter()
	{
		Predicate = this.WhenValueChanged(x => x.Text).Select(text => (Func<AnimeModel, bool>)(anime => UserListQuery.MatchesTitle(anime, text)));
	}

	[ObservableProperty]
	public partial string Text { get; set; } = "";

	public IObservable<Func<AnimeModel, bool>> Predicate { get; }

	public bool IsActive => !string.IsNullOrWhiteSpace(Text);

	partial void OnTextChanged(string value) => OnPropertyChanged(nameof(IsActive));
}

/// <summary>A genre to filter by, ticked or not.</summary>
public sealed partial class GenreOption(string name) : ObservableObject
{
	public string Name { get; } = name;

	[ObservableProperty]
	public partial bool IsSelected { get; set; }
}

/// <summary>Anime with every genre ticked; the genres offered are those of the list's anime.</summary>
public sealed partial class GenreFilter : ObservableObject, IFilter<AnimeModel>
{
	private readonly BehaviorSubject<Func<AnimeModel, bool>> _predicate = new(_ => true);

	public ObservableCollection<GenreOption> Options { get; } = [];

	public IObservable<Func<AnimeModel, bool>> Predicate => _predicate;

	public bool IsActive => SelectedCount > 0;

	public int SelectedCount => Options.Count(x => x.IsSelected);

	/// <summary>e.g. "Genres", or "Genres (2)" with two ticked.</summary>
	public string Label => SelectedCount > 0 ? $"Genres ({SelectedCount})" : "Genres";

	/// <summary>Offers the genres of these anime, alphabetically; ticked ones stay ticked.</summary>
	public void SetGenres(IEnumerable<AnimeModel> anime)
	{
		var selected = Options.Where(x => x.IsSelected).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var option in Options)
		{
			option.PropertyChanged -= OnOptionChanged;
		}

		Options.Clear();
		foreach (var name in anime.SelectMany(x => x.Genres).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
		{
			var option = new GenreOption(name) { IsSelected = selected.Contains(name) };
			option.PropertyChanged += OnOptionChanged;
			Options.Add(option);
		}

		Update();
	}

	public void Clear()
	{
		foreach (var option in Options)
		{
			option.IsSelected = false;
		}
	}

	private void OnOptionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(GenreOption.IsSelected))
		{
			Update();
		}
	}

	private void Update()
	{
		var selected = Options.Where(x => x.IsSelected).Select(x => x.Name).ToList();
		_predicate.OnNext(anime => selected.All(genre => anime.Genres.Contains(genre, StringComparer.OrdinalIgnoreCase)));
		OnPropertyChanged(nameof(SelectedCount));
		OnPropertyChanged(nameof(IsActive));
		OnPropertyChanged(nameof(Label));
	}
}

/// <summary>How the years are filtered: not at all, one year, or a range (either end may be open).</summary>
public enum YearFilterMode
{
	Any,
	Year,
	Range,
}

/// <summary>Anime that aired in a season, in a year or a range of years, or both.</summary>
public sealed partial class SeasonFilter : ObservableObject, IFilter<AnimeModel>
{
	private readonly BehaviorSubject<Func<AnimeModel, bool>> _predicate = new(_ => true);

	public SeasonFilter()
	{
		Season = Seasons[0];
	}

	public IReadOnlyList<FilterOption<AnimeSeason?>> Seasons { get; } =
	[
		new("Any season", null),
		new("Winter", AnimeSeason.Winter),
		new("Spring", AnimeSeason.Spring),
		new("Summer", AnimeSeason.Summer),
		new("Fall", AnimeSeason.Fall),
	];

	/// <summary>The latest year to offer: next year's, for anime planned that haven't started.</summary>
	public int LatestYear { get; } = DateTime.Now.Year + 1;

	[ObservableProperty]
	public partial FilterOption<AnimeSeason?>? Season { get; set; }

	[ObservableProperty]
	public partial int? MinimumYear { get; set; }

	[ObservableProperty]
	public partial int? MaximumYear { get; set; }

	/// <summary>
	/// Whether the years filter on one year (<see cref="Year"/>) or a range (<see cref="MinimumYear"/> to
	/// <see cref="MaximumYear"/>). Any clears the years; Year narrows a range to its start.
	/// </summary>
	[ObservableProperty]
	public partial YearFilterMode YearMode { get; set; }

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

	public IObservable<Func<AnimeModel, bool>> Predicate => _predicate;

	public bool IsActive => Season?.Value is not null || MinimumYear is not null || MaximumYear is not null;

	public void Clear()
	{
		Season = Seasons[0];
		YearMode = YearFilterMode.Any;
		MinimumYear = MaximumYear = null;
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

	partial void OnSeasonChanged(FilterOption<AnimeSeason?>? value) => Update();

	partial void OnMinimumYearChanged(int? value)
	{
		OnPropertyChanged(nameof(Year));
		Update();
	}

	partial void OnMaximumYearChanged(int? value) => Update();

	private void Update()
	{
		var (season, from, to) = (Season?.Value, MinimumYear, MaximumYear);
		_predicate.OnNext(anime => UserListQuery.MatchesSeason(anime, season, from, to));
		OnPropertyChanged(nameof(IsActive));
	}
}
