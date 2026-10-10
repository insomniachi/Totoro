using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Totoro.Core;
using Totoro.Core.Dialogs;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.ViewModels;

namespace Totoro.Commands;

/// <summary>
/// Commands that act on an anime (edit its tracking, ...), shared by every view that shows anime. Each takes the
/// <see cref="AnimeModel"/> as its parameter. Bind them straight from XAML, no view model needed:
/// <c>Command="{x:Bind commands:AnimeCommands.Shared.EditTrackingCommand}"</c>.
/// </summary>
public partial class AnimeCommands(IDialogService dialogs, IFactory<IAnimeTracker, Guid> trackers, ILogger<AnimeCommands> logger)
{
	/// <summary>The app-wide instance (registered as a singleton), for x:Bind from XAML.</summary>
	public static AnimeCommands Shared => Locator.GetRequiredService<AnimeCommands>();

	/// <summary>Edits the anime's tracking in a dialog and saves it to the selected tracker.</summary>
	[RelayCommand]
	private async Task EditTracking(AnimeModel anime)
	{
		if (await dialogs.ShowAsync<TrackingDialogViewModel, Tracking>(anime) is not { } tracking)
		{
			return;
		}

		if (trackers.CreateDefault() is not { } tracker)
		{
			return;
		}

		try
		{
			// Views react to Tracking changing, e.g. the user list re-filters and moves the card to its new tab.
			anime.Tracking = await tracker.Update(anime.Id, tracking);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to update tracking for {Anime}", anime.Title);
		}
	}
}
