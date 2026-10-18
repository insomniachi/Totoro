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

/// <summary>Anime that aired in a season, a year, or both.</summary>
public sealed partial class SeasonFilter : ObservableObject, IFilter<AnimeModel>
{
	private readonly BehaviorSubject<Func<AnimeModel, bool>> _predicate = new(_ => true);

	public SeasonFilter()
	{
		Season = Seasons[0];
		Year = Years[0];
	}

	public IReadOnlyList<FilterOption<AnimeSeason?>> Seasons { get; } =
	[
		new("Any season", null),
		new("Winter", AnimeSeason.Winter),
		new("Spring", AnimeSeason.Spring),
		new("Summer", AnimeSeason.Summer),
		new("Fall", AnimeSeason.Fall),
	];

	/// <summary>"Any year", then the years of the list's anime, latest first.</summary>
	public ObservableCollection<FilterOption<int?>> Years { get; } = [new("Any year", null)];

	[ObservableProperty]
	public partial FilterOption<AnimeSeason?>? Season { get; set; }

	[ObservableProperty]
	public partial FilterOption<int?>? Year { get; set; }

	public IObservable<Func<AnimeModel, bool>> Predicate => _predicate;

	public bool IsActive => Season?.Value is not null || Year?.Value is not null;

	/// <summary>Offers the years of these anime; the year picked stays picked if it's still one of them.</summary>
	public void SetYears(IEnumerable<AnimeModel> anime)
	{
		var picked = Year?.Value;
		while (Years.Count > 1)
		{
			Years.RemoveAt(1);
		}

		foreach (var year in anime.Select(x => x.Season?.Year ?? 0).Where(x => x > 1900).Distinct().OrderDescending())
		{
			Years.Add(new(year.ToString(System.Globalization.CultureInfo.InvariantCulture), year));
		}

		Year = Years.FirstOrDefault(x => x.Value == picked) ?? Years[0];
	}

	public void Clear()
	{
		Season = Seasons[0];
		Year = Years[0];
	}

	partial void OnSeasonChanged(FilterOption<AnimeSeason?>? value) => Update();

	partial void OnYearChanged(FilterOption<int?>? value) => Update();

	private void Update()
	{
		var (season, year) = (Season?.Value, Year?.Value);
		_predicate.OnNext(anime => UserListQuery.MatchesSeason(anime, season, year));
		OnPropertyChanged(nameof(IsActive));
	}
}
