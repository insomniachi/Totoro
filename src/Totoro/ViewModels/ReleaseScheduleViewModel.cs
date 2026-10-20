using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>A day on the release calendar, with the episodes airing on it.</summary>
/// <param name="IsLoading">True while the list it comes from is loading: it shows a placeholder.</param>
public sealed record CalendarDay(DateTime Date, bool IsInMonth, IReadOnlyList<ScheduledEpisode> Episodes, bool IsLoading)
{
	public bool IsToday => Date == DateTime.Today;

	private const int MaxBands = 3;

	/// <summary>The episodes that get a band of the day, each with its anime's cover: the first few.</summary>
	public IReadOnlyList<ScheduledEpisode> Bands => Episodes.Count > MaxBands ? [.. Episodes.Take(MaxBands)] : Episodes;

	/// <summary>How many episodes don't get a band; the day lists them all in a flyout.</summary>
	public int MoreCount => Episodes.Count - Bands.Count;

	public string MoreLabel => $"+{MoreCount} more";
}

/// <summary>
/// A month calendar of when the episodes of the anime the user is watching air (see <see cref="ReleaseScheduleService"/>),
/// from this month on.
/// </summary>
public sealed partial class ReleaseScheduleViewModel(ReleaseScheduleService schedule, AnimeTrackingService tracking) : NavigatableViewModel
{
	private List<AnimeModel> _watching = [];
	private bool _loading;

	private static DateTime ThisMonth => new(DateTime.Today.Year, DateTime.Today.Month, 1);

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Title))]
	[NotifyCanExecuteChangedFor(nameof(PreviousMonthCommand))]
	public partial DateTime Month { get; set; } = ThisMonth;

	[ObservableProperty]
	public partial IReadOnlyList<CalendarDay> Days { get; set; } = [];

	public string Title => Month.ToString("Y", CultureInfo.CurrentCulture);

	/// <summary>The weekdays' short names, from Monday.</summary>
	public IReadOnlyList<string> DayNames { get; } =
		[.. Enumerable.Range(0, 7).Select(i => CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[((int)FirstDayOfWeek + i) % 7])];

	private const DayOfWeek FirstDayOfWeek = DayOfWeek.Monday;

	/// <summary>Whether the user is signed in, and whether the list loaded; for the page's InfoBars.</summary>
	public ListLoadState LoadState => field ??= new(tracking, LoadWatchingAsync, "to see when the shows you're watching air");

	protected override Task OnNavigatedToInternalAsync()
	{
		Month = ThisMonth;
		return LoadState.LoadAsync();
	}

	private async Task LoadWatchingAsync()
	{
		// The month laid out straight away, its days shimmering until the list arrives.
		_loading = true;
		_watching = [];
		UpdateDays();
		try
		{
			_watching = await schedule.GetWatchingAsync();
		}
		finally
		{
			_loading = false;
			UpdateDays();
		}
	}

	private bool CanGoToPreviousMonth() => Month > ThisMonth;

	[RelayCommand(CanExecute = nameof(CanGoToPreviousMonth))]
	private void PreviousMonth()
	{
		Month = Month.AddMonths(-1);
		UpdateDays();
	}

	[RelayCommand]
	private void NextMonth()
	{
		Month = Month.AddMonths(1);
		UpdateDays();
	}

	/// <summary>The weeks the month is in, whole.</summary>
	private void UpdateDays()
	{
		var start = Month.AddDays(-(((int)Month.DayOfWeek - (int)FirstDayOfWeek + 7) % 7));
		var count = (int)Math.Ceiling((Month.AddMonths(1) - start).TotalDays / 7) * 7;
		var episodes = ReleaseScheduleService.Between(_watching, start, start.AddDays(count)).ToLookup(x => x.AiringAt.Date);
		Days = [.. Enumerable.Range(0, count).Select(i => start.AddDays(i)).Select(d => new CalendarDay(d, d.Month == Month.Month, [.. episodes[d]], _loading))];
	}
}
