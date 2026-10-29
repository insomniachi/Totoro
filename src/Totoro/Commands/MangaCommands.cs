using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Totoro.Core;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;
using Totoro.Core.Navigation;
using Totoro.Core.Services;
using Totoro.UI.Navigation;
using Totoro.ViewModels.Manga;

namespace Totoro.Commands;

/// <summary>
/// Commands that act on a manga, shared by every view that shows manga, as <see cref="AnimeCommands"/> are for anime.
/// Each takes the <see cref="MangaModel"/>: <c>Command="{x:Bind commands:MangaCommands.Shared.ReadCommand}"</c>.
/// </summary>
public partial class MangaCommands(IDialogService dialogs, MangaTrackingService tracking, MangaReadingService reading, ILogger<MangaCommands> logger)
{
	public static MangaCommands Shared => Locator.GetRequiredService<MangaCommands>();

	// Navigator keys of the main window's page area and side pane (see MainWindow.xaml).
	private const string MainNavigatorKey = "main";
	private const string PaneNavigatorKey = "pane";

	[RelayCommand]
	private static async Task ShowDetailsAsync(MangaModel manga)
	{
		if (Navigators.TryGetNavigator(PaneNavigatorKey, out var pane))
		{
			await pane.NavigateToDataAsync(manga);
		}
	}

	/// <summary>
	/// Opens the next chapter to read in the reader. When that can't be worked out (no source, or the source's match
	/// isn't certain), shows the manga's details instead, where the user can pick.
	/// </summary>
	[RelayCommand]
	private async Task ReadAsync(MangaModel manga)
	{
		try
		{
			if (reading.Source is { } source && await reading.FindAsync(source, manga, CancellationToken.None) is { } match)
			{
				var chapters = await MangaReadingService.GetChaptersAsync(source, match.Id, CancellationToken.None);
				if (MangaReadingService.NextChapter(chapters, manga.Tracking) is { } next)
				{
					await OpenReaderAsync(new ReaderRequest(manga, source.Id, chapters, chapters.IndexOf(next)));
					return;
				}
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't find {Manga}'s next chapter", manga.Title);
		}

		await ShowDetailsAsync(manga);
	}

	/// <summary>Opens the reader at a chapter, in the main window's page area (Back returns from it).</summary>
	public static async Task OpenReaderAsync(ReaderRequest request)
	{
		if (Navigators.TryGetNavigator(PaneNavigatorKey, out var pane) && pane is IDismissibleNavigator dismissible)
		{
			dismissible.Close();
		}

		if (Navigators.TryGetNavigator(MainNavigatorKey, out var main))
		{
			await main.NavigateToViewModelAsync(typeof(ReaderViewModel), request);
		}
	}

	/// <summary>Edits the manga's list entry in a dialog and saves it.</summary>
	[RelayCommand]
	private async Task EditTrackingAsync(MangaModel manga)
	{
		if (await dialogs.ShowAsync<MangaTrackingDialogViewModel, MangaTracking>(manga) is not { } changes)
		{
			return;
		}

		try
		{
			await tracking.UpdateAsync(manga, changes);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to update tracking for {Manga}", manga.Title);
		}
	}

	[RelayCommand]
	private async Task PlanToReadAsync(MangaModel manga)
	{
		if (manga.Tracking?.Status is not null)
		{
			return;
		}

		try
		{
			await tracking.UpdateAsync(manga, new MangaTracking { Status = ListItemStatus.PlanToWatch });
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to add {Manga} to planning", manga.Title);
		}
	}
}
