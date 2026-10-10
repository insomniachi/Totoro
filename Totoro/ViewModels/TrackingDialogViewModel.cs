using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Dialogs;
using Totoro.Core.Module.Anime;

namespace Totoro.ViewModels;

public sealed record StatusOption(string Label, ListItemStatus Value)
{
	public override string ToString() => Label;
}

/// <summary>Edits an anime's list entry. Returns the edited <see cref="Tracking"/>; saving it is up to the caller.</summary>
public partial class TrackingDialogViewModel : DialogViewModel<Tracking>
{
	public TrackingDialogViewModel(AnimeModel anime)
	{
		Title = anime.Title;
		PrimaryButtonText = "Save";

		MaxEpisodes = anime.TotalEpisodes is > 0 and var total ? total : double.MaxValue;

		var tracking = anime.Tracking;
		Status = Statuses.FirstOrDefault(x => x.Value == tracking?.Status) ?? Statuses[0];
		WatchedEpisodes = tracking?.WatchedEpisodes ?? 0;
		Score = tracking?.Score ?? 0;
		StartDate = tracking?.StartDate;
		FinishDate = tracking?.FinishDate;
	}

	public IReadOnlyList<StatusOption> Statuses { get; } =
	[
		new("Watching", ListItemStatus.Watching),
		new("Plan To Watch", ListItemStatus.PlanToWatch),
		new("Completed", ListItemStatus.Completed),
		new("On Hold", ListItemStatus.OnHold),
		new("Dropped", ListItemStatus.Dropped),
		new("Rewatching", ListItemStatus.Rewatching),
	];

	/// <summary>The anime's episode count, or unbounded when it isn't known (e.g. still airing).</summary>
	public double MaxEpisodes { get; }

	[ObservableProperty]
	public partial StatusOption Status { get; set; }

	// double rather than int: NumberBox works in doubles and uses NaN for an empty box.
	[ObservableProperty]
	public partial double WatchedEpisodes { get; set; }

	/// <summary>0-10, 0 meaning not scored.</summary>
	[ObservableProperty]
	public partial double Score { get; set; }

	[ObservableProperty]
	public partial DateTimeOffset? StartDate { get; set; }

	[ObservableProperty]
	public partial DateTimeOffset? FinishDate { get; set; }

	[ObservableProperty]
	public partial string? ValidationError { get; set; }

	partial void OnStatusChanged(StatusOption value)
	{
		// Fill in what the status implies, without overwriting anything already set.
		switch (value.Value)
		{
			case ListItemStatus.Watching or ListItemStatus.Rewatching:
				StartDate ??= DateTimeOffset.Now.Date;
				break;
			case ListItemStatus.Completed:
				StartDate ??= DateTimeOffset.Now.Date;
				FinishDate ??= DateTimeOffset.Now.Date;
				if (MaxEpisodes < double.MaxValue)
				{
					WatchedEpisodes = MaxEpisodes;
				}
				break;
		}
	}

	partial void OnStartDateChanged(DateTimeOffset? value) => Validate();

	partial void OnFinishDateChanged(DateTimeOffset? value) => Validate();

	private void Validate()
	{
		ValidationError = StartDate is { } start && FinishDate is { } finish && finish.Date < start.Date
			? "The finish date can't be before the start date."
			: null;
		CanConfirm = ValidationError is null;
	}

	public override Task<bool> ConfirmAsync()
	{
		Result = new Tracking
		{
			Status = Status.Value,
			WatchedEpisodes = double.IsNaN(WatchedEpisodes) ? null : (int)WatchedEpisodes,
			Score = double.IsNaN(Score) || Score <= 0 ? null : (int)Score,
			StartDate = StartDate?.Date,
			FinishDate = FinishDate?.Date,
		};

		return Task.FromResult(true);
	}
}
