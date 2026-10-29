using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;

namespace Totoro.ViewModels.Manga;

/// <summary>Edits a manga's list entry. Returns the edited <see cref="MangaTracking"/>; saving it is up to the caller.</summary>
public partial class MangaTrackingDialogViewModel : DialogViewModel<MangaTracking>
{
	public MangaTrackingDialogViewModel(MangaModel manga)
	{
		Title = manga.Title;
		PrimaryButtonText = "Save";
		MaxChapters = manga.TotalChapters is > 0 and var chapters ? chapters : double.MaxValue;
		MaxVolumes = manga.TotalVolumes is > 0 and var volumes ? volumes : double.MaxValue;

		var tracking = manga.Tracking;
		// Not on the list yet: being added, most likely to read later.
		Status = Statuses.FirstOrDefault(x => x.Value == tracking?.Status) ?? Statuses.First(x => x.Value == ListItemStatus.PlanToWatch);
		ChaptersRead = tracking?.ChaptersRead ?? 0;
		VolumesRead = tracking?.VolumesRead ?? 0;
		Score = tracking?.Score ?? 0;
		StartDate = tracking?.StartDate;
		FinishDate = tracking?.FinishDate;
	}

	public IReadOnlyList<StatusOption> Statuses { get; } =
	[
		.. new[] { ListItemStatus.Watching, ListItemStatus.PlanToWatch, ListItemStatus.Completed, ListItemStatus.OnHold, ListItemStatus.Dropped, ListItemStatus.Rewatching }
			.Select(x => new StatusOption(MediaText.Status(x, Core.Navigation.MediaMode.Manga), x))
	];

	public double MaxChapters { get; }

	public double MaxVolumes { get; }

	[ObservableProperty]
	public partial StatusOption Status { get; set; }

	// doubles: NumberBox works in doubles and uses NaN for an empty box.
	[ObservableProperty]
	public partial double ChaptersRead { get; set; }

	[ObservableProperty]
	public partial double VolumesRead { get; set; }

	/// <summary>Out of 10; 0 is no score.</summary>
	[ObservableProperty]
	public partial double Score { get; set; }

	[ObservableProperty]
	public partial DateTimeOffset? StartDate { get; set; }

	[ObservableProperty]
	public partial DateTimeOffset? FinishDate { get; set; }

	partial void OnStatusChanged(StatusOption value)
	{
		// Completing it finishes it; as on the anime dialog.
		if (value.Value == ListItemStatus.Completed && MaxChapters < double.MaxValue)
		{
			ChaptersRead = MaxChapters;
			FinishDate ??= DateTimeOffset.Now;
		}
	}

	public override Task<bool> ConfirmAsync()
	{
		Result = new MangaTracking
		{
			Status = Status.Value,
			ChaptersRead = double.IsNaN(ChaptersRead) ? null : (int)ChaptersRead,
			VolumesRead = double.IsNaN(VolumesRead) ? null : (int)VolumesRead,
			Score = double.IsNaN(Score) ? null : Score,
			StartDate = StartDate?.Date,
			FinishDate = FinishDate?.Date,
		};
		return Task.FromResult(true);
	}
}
