using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Relations;
using Totoro.Playback;

namespace Totoro.ViewModels;

/// <summary>
/// Asks which of an anime's episodes to download: from the next one the user hasn't watched to the latest that's aired.
/// Downloading them is up to the caller (see <see cref="DownloadService.DownloadEpisodesAsync"/>).
/// </summary>
public sealed partial class DownloadEpisodesDialogViewModel : DialogViewModel<EpisodeRange>
{
	public DownloadEpisodesDialogViewModel(AnimeModel anime)
	{
		Title = $"Download {anime.Title}";
		PrimaryButtonText = "Download";

		// The aired episodes when known; otherwise any number (the provider says which it has).
		var last = EpisodeAvailability.AiredEpisodes(anime, DateTime.Now) is > 0 and var aired ? aired : anime.TotalEpisodes ?? 0;
		MaxEpisode = last > 0 ? last : double.MaxValue;
		var next = WatchService.NextEpisode(anime);
		From = last > 0 ? Math.Min(next, last) : next;
		To = last > 0 ? last : From;
	}

	/// <summary>The latest episode that's aired, or unbounded when that isn't known.</summary>
	public double MaxEpisode { get; }

	// double rather than int: NumberBox works in doubles and uses NaN for an empty box.
	[ObservableProperty]
	public partial double From { get; set; }

	[ObservableProperty]
	public partial double To { get; set; }

	/// <summary>e.g. "Episodes 5 to 12 (8)", or why the range can't be downloaded; never null.</summary>
	[ObservableProperty]
	public partial string Summary { get; private set; } = "";

	partial void OnFromChanged(double value) => Validate();

	partial void OnToChanged(double value) => Validate();

	private void Validate()
	{
		CanConfirm = !double.IsNaN(From) && !double.IsNaN(To) && From >= 1 && To >= From;
		Summary = !CanConfirm
			? "Choose the first episode, then a last one that isn't before it."
			: From == To
				? $"Episode {From}"
				: $"Episodes {From} to {To} ({To - From + 1}), one after the other";
	}

	public override Task<bool> ConfirmAsync()
	{
		Result = new EpisodeRange((int)From, (int)To);
		return Task.FromResult(true);
	}
}
