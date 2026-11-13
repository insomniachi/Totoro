using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Modules.AnimeDatabase.Anilist;
using Totoro.Modules.AnimeDatabase.MyAnimeList;
using Totoro.Modules.AnimeProviders.Downloads;
using Totoro.Playback;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>
/// The anime in the download folder, and the episodes downloaded or downloading (see <see cref="DownloadService"/>); where
/// they go is in Settings.
/// </summary>
public sealed partial class DownloadsViewModel(DownloadService downloads, IDialogService dialogs, WatchService watch,
											   IFactory<IAnimeMetadataProvider, Guid> databases, IFactory<IAnimeProvider, Guid> providers,
											   ILogger<DownloadsViewModel> logger) : NavigatableViewModel
{
	/// <summary>
	/// The downloaded episode to continue from: the first at or after the next to watch by its tracking; the first
	/// downloaded when it isn't tracked here; null when every downloaded one is watched.
	/// </summary>
	public async Task<int?> NextDownloadedAsync(DownloadedAnime anime)
	{
		if (await FindAnimeAsync(anime) is not { } model)
		{
			return anime.EpisodeNumbers.Count > 0 ? anime.EpisodeNumbers[0] : null;
		}

		var next = await watch.NextToWatchAsync(model);
		return anime.EpisodeNumbers.Where(x => x >= next).Select(x => (int?)x).FirstOrDefault();
	}

	/// <summary>
	/// Plays the next episode to watch by the anime's tracking: from the download folder when it's there, else from its
	/// provider. The first downloaded one when it isn't tracked here.
	/// </summary>
	public async Task ContinueAsync(DownloadedAnime anime)
	{
		if (await FindAnimeAsync(anime) is not { } model)
		{
			if (anime.EpisodeNumbers.Count > 0)
			{
				await PlayAsync(anime, anime.EpisodeNumbers[0]);
			}

			return;
		}

		var next = await watch.NextToWatchAsync(model);
		await watch.WatchAsync(model, anime.EpisodeNumbers.FirstOrDefault(x => x >= next, next), fromDownloads: true);
	}

	/// <summary>
	/// Plays the anime's episode <paramref name="number"/> from the download folder. Tracked, like any episode, when its
	/// database has it; else (offline, or a folder without ids) just played.
	/// </summary>
	public async Task PlayAsync(DownloadedAnime anime, int number)
	{
		if (await FindAnimeAsync(anime) is { } model)
		{
			await watch.WatchAsync(model, number, fromDownloads: true);
			return;
		}

		// Not tracked: a tracker can't be updated for an anime it wasn't found in.
		var untracked = new AnimeModel
		{
			Title = anime.Title,
			Image = anime.Cover,
			ExternalIds = new AnimeId { Anilist = anime.Info?.AnilistId ?? 0, MyAnimeList = anime.Info?.MalId ?? 0 },
		};
		if (providers.Create(DownloadsModule.Id) is not { } provider ||
			(await provider.GetEpisodesAsync(anime.Name, default)).FirstOrDefault(x => x.Number == number) is not { } episode ||
			(await provider.GetServersAsync(episode, default)).FirstOrDefault() is not { } server)
		{
			Error = $"Episode {number} of {anime.Title} isn't in its folder.";
			return;
		}

		Error = "";
		await watch.WatchFromAsync(untracked, number, provider, episode, server, track: false);
	}

	/// <summary>The anime in the selected database, by the ids in its info file; null when it can't be had.</summary>
	private async Task<AnimeModel?> FindAnimeAsync(DownloadedAnime anime)
	{
		if (databases.CreateDefault() is not { } database)
		{
			return null;
		}

		// shortcut: only AniList and MyAnimeList are looked up by id; other databases (plugins) play untracked until mapped.
		var id = database.Id == AnilistModule.Id ? anime.Info?.AnilistId
			: database.Id == MyAnimeListModule.Id ? anime.Info?.MalId
			: null;
		if (id is null)
		{
			return null;
		}

		try
		{
			return await database.GetAnimeAsync(id.Value);
		}
		catch (Exception ex)
		{
			// E.g. offline: the episode still plays, untracked.
			logger.LogWarning(ex, "Couldn't get {Anime} from {Database}", anime.Title, database.Name);
			return null;
		}
	}

	public ObservableCollection<DownloadItem> Downloads => downloads.Downloads;

	public ObservableCollection<DownloadedAnime> Library => downloads.Library;

	/// <summary>Why editing or deleting an anime failed; empty, never null, when it didn't.</summary>
	[ObservableProperty] public partial string Error { get; set; } = "";

	public void OpenFolder() => downloads.OpenFolder();

	public void Refresh() => Run(downloads.RefreshLibrary);

	public async Task EditInfoAsync(DownloadedAnime anime)
	{
		if (await dialogs.ShowAsync<DownloadInfoDialogViewModel, DownloadInfo>(anime) is { } info)
		{
			Run(() => downloads.SaveInfo(anime.Name, info));
		}
	}

	public void Delete(DownloadedAnime anime) => Run(() => downloads.Delete(anime.Name));

	/// <summary>What finding openings and endings is doing, or what it found; empty, never null, when there's nothing to say.</summary>
	[ObservableProperty] public partial string Status { get; set; } = "";

	/// <summary>Finds the openings and endings of the anime's episodes that don't have them yet.</summary>
	public async Task FindSegmentsAsync(DownloadedAnime anime)
	{
		Status = $"Finding the openings and endings of {anime.Title}'s episodes…";
		try
		{
			var (looked, found) = await downloads.FindMissingSegmentsAsync(anime.Name);
			Error = "";
			Status = looked == 0
				? $"All of {anime.Title}'s episodes have them already."
				: $"Found the opening and ending of {found} of {looked} episodes of {anime.Title}.";
		}
		catch (Exception ex) when (ex is DownloadException or JsonException or IOException or UnauthorizedAccessException)
		{
			Status = "";
			Error = ex.Message;
		}
	}

	protected override Task OnNavigatedToInternalAsync()
	{
		Refresh();
		return Task.CompletedTask;
	}

	private void Run(Action action)
	{
		try
		{
			action();
			Error = "";
		}
		catch (Exception ex) when (ex is DownloadException or IOException or UnauthorizedAccessException or OperationCanceledException)
		{
			Error = ex.Message;
		}
	}
}
