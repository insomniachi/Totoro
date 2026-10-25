using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Windows.Globalization.NumberFormatting;

namespace Totoro.ViewModels;

public sealed record StatusOption(string Label, ListItemStatus Value)
{
	public override string ToString() => Label;
}

/// <summary>Edits an anime's list entry. Returns the edited <see cref="Tracking"/>; saving it is up to the caller.</summary>
public partial class TrackingDialogViewModel : DialogViewModel<Tracking>
{
	private readonly ILogger<TrackingDialogViewModel> _logger;

	public TrackingDialogViewModel(AnimeModel anime, AnimeTrackingService trackingService, ILogger<TrackingDialogViewModel> logger)
	{
		_logger = logger;
		Title = anime.Title;
		PrimaryButtonText = "Save";

		MaxEpisodes = anime.TotalEpisodes is > 0 and var total ? total : double.MaxValue;

		var tracking = anime.Tracking;
		Status = Statuses.FirstOrDefault(x => x.Value == tracking?.Status) ?? Statuses[0];
		WatchedEpisodes = tracking?.WatchedEpisodes ?? 0;
		Form = TrackingForm.Default;
		Score = tracking?.Score ?? 0;
		StartDate = tracking?.StartDate;
		FinishDate = tracking?.FinishDate;

		_ = LoadFormAsync(anime, trackingService);
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

	/// <summary>On the tracker's scale (<see cref="TrackingForm.ScoreMax"/>), 0 (or -1, a cleared rating) meaning not scored.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(StarValue), nameof(SmileyIndex))]
	public partial double Score { get; set; }

	/// <summary>The tracker's score scale and own fields; the default (out of 10, no fields) until it has said.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ScoreHeader), nameof(ScoreFormatter), nameof(StarCount), nameof(IsNumberScore), nameof(IsStarScore), nameof(IsSmileyScore))]
	public partial TrackingForm Form { get; set; }

	public string ScoreHeader => Form.ScoreStep < 1 ? $"Score (0-{Form.ScoreMax:0.0})" : $"Score (0-{Form.ScoreMax:0})";

	/// <summary>Shows as many decimals as the scale has, e.g. 7.5 rather than 7.50 or 8.</summary>
	public DecimalFormatter ScoreFormatter => new()
	{
		FractionDigits = Form.ScoreStep < 1 ? 1 : 0,
		NumberRounder = new IncrementNumberRounder { Increment = Form.ScoreStep },
	};

	public int StarCount => (int)Form.ScoreMax;

	/// <summary>The score for RatingControl, which shows 0 as one star and takes -1 for no rating.</summary>
	public double StarValue
	{
		get => Score > 0 ? Score : -1;
		set => Score = value;
	}

	public bool IsNumberScore => Form.ScoreStyle == ScoreStyle.Number;

	public bool IsStarScore => Form.ScoreStyle == ScoreStyle.Stars;

	public bool IsSmileyScore => Form.ScoreStyle == ScoreStyle.Smileys;

	/// <summary>The selected face, from unhappy (0) to happy (2); -1 for none.</summary>
	public int SmileyIndex
	{
		get => Score is >= 1 and <= 3 ? (int)Score - 1 : -1;
		set => Score = value + 1;
	}

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

	/// <summary>
	/// Asks the anime's tracker for its form, putting the score on its scale. If it can't say (e.g. offline), the shared
	/// fields can still be saved.
	/// </summary>
	private async Task LoadFormAsync(AnimeModel anime, AnimeTrackingService trackingService)
	{
		try
		{
			var form = await trackingService.GetTracker(anime).GetFormAsync(anime.Id, CancellationToken.None);
			var score = FromTen(Score, form);
			// The form first: the score box clamps to its maximum.
			Form = form;
			Score = score;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Couldn't get the tracking form for {Anime}", anime.Title);
		}
	}

	// Faces aren't evenly spaced out of 10: AniList saves them as 35, 60 and 85 out of 100, and reads up to 35 as unhappy
	// and up to 60 as neutral.
	private static readonly double[] s_smileyScores = [3.5, 6, 8.5];

	/// <summary>A score out of 10 on the form's scale.</summary>
	internal static double FromTen(double score, TrackingForm form)
	{
		if (score <= 0)
		{
			return 0;
		}

		return form.ScoreStyle == ScoreStyle.Smileys
			? score <= 3.5 ? 1 : score <= 6 ? 2 : 3
			: Math.Round(score * form.ScoreMax / 10 / form.ScoreStep) * form.ScoreStep;
	}

	/// <summary>A score on the form's scale out of 10.</summary>
	internal static double ToTen(double score, TrackingForm form)
	{
		return form.ScoreStyle == ScoreStyle.Smileys ? s_smileyScores[Math.Clamp((int)score, 1, 3) - 1] : score * 10 / form.ScoreMax;
	}

	public override Task<bool> ConfirmAsync()
	{
		Result = new Tracking
		{
			Status = Status.Value,
			WatchedEpisodes = double.IsNaN(WatchedEpisodes) ? null : (int)WatchedEpisodes,
			Score = double.IsNaN(Score) || Score <= 0 ? null : ToTen(Score, Form),
			StartDate = StartDate?.Date,
			FinishDate = FinishDate?.Date,
			Fields = Form.Fields,
		};

		return Task.FromResult(true);
	}
}
