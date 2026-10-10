using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Totoro.Core;
using Totoro.Core.Dialogs;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.Playback;
using Totoro.UI.Navigation;
using Totoro.ViewModels;

namespace Totoro.Commands;

/// <summary>
/// Commands that act on an anime (edit its tracking, ...), shared by every view that shows anime. Each takes the
/// <see cref="AnimeModel"/> as its parameter. Bind them straight from XAML, no view model needed:
/// <c>Command="{x:Bind commands:AnimeCommands.Shared.EditTrackingCommand}"</c>.
/// </summary>
public partial class AnimeCommands(IDialogService dialogs, IFactory<IAnimeTracker, Guid> trackers, WatchService watch, ILogger<AnimeCommands> logger)
{
	/// <summary>The app-wide instance (registered as a singleton), for x:Bind from XAML.</summary>
	public static AnimeCommands Shared => Locator.GetRequiredService<AnimeCommands>();

	/// <summary>Navigator key of the main window's side pane (see MainWindow.xaml).</summary>
	private const string PaneNavigatorKey = "pane";

	/// <summary>Shows the anime's details in the main window's side pane.</summary>
	[RelayCommand]
	private async Task ShowDetailsAsync(AnimeModel anime)
	{
		if (Navigators.TryGetNavigator(PaneNavigatorKey, out var pane))
		{
			await pane.NavigateToDataAsync(anime);
		}
	}

	/// <summary>Plays the next episode to watch (after the last one watched); see <see cref="WatchService"/>.</summary>
	[RelayCommand(AllowConcurrentExecutions = true)]
	// Concurrent: playback lasts until the player closes, and a command that can't run concurrently is disabled until
	// then, which would disable every play button. Starting another episode replaces the one playing.
	private Task WatchAsync(AnimeModel anime) => watch.WatchNextAsync(anime);

	/// <summary>Plays a particular episode, from where it was left off if it wasn't finished.</summary>
	[RelayCommand(AllowConcurrentExecutions = true)]
	private Task WatchEpisodeAsync(EpisodeToWatch episode) => watch.WatchAsync(episode.Anime, episode.Number);

	/// <summary>Edits the anime's own settings for watching it (provider, skipping...) in a dialog.</summary>
	[RelayCommand]
	private Task EditSettingsAsync(AnimeModel anime) => dialogs.ShowAsync<AnimeSettingsDialogViewModel, bool>(anime);

	/// <summary>Edits the anime's tracking in a dialog and saves it to the selected tracker.</summary>
	[RelayCommand]
	private async Task EditTrackingAsync(AnimeModel anime)
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
			anime.Tracking = await tracker.UpdateAsync(anime.Id, tracking);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to update tracking for {Anime}", anime.Title);
		}
	}
}
