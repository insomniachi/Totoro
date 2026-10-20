using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;

namespace Totoro.Core.Services;

/// <summary>
/// Changes the user's list, for the UI and agent tools alike: saves to the tracker of the database the anime came from,
/// updates the anime, and sends an <see cref="AnimeTrackingChangedMessage"/> so every view showing it follows. Then, if
/// <see cref="AnimeDatabasePreferences.SyncOtherLists"/> is on, makes the same change on the user's other lists (the
/// other databases they're signed in to), finding the anime's id there; a failure there is reported with an
/// <see cref="OtherListSyncFailedMessage"/> rather than failing the change.
/// </summary>
public sealed class AnimeTrackingService(
	IFactory<IAnimeTracker, Guid> trackers,
	IEnumerable<IAccount> accounts,
	IPreferences preferences,
	ILogger<AnimeTrackingService> logger)
{
	/// <summary>
	/// Saves <paramref name="changes"/> to the anime's list entry (adding it to the list if it isn't on it). Only the
	/// fields set in it change; returns the whole entry as it is now.
	/// </summary>
	public async Task<Tracking> UpdateAsync(AnimeModel anime, Tracking changes)
	{
		var main = GetTracker(anime);
		var saved = await main.UpdateAsync(anime.Id, changes);

		// Trackers return what they changed; the rest stays as it was.
		var current = anime.Tracking;
		var tracking = new Tracking
		{
			Status = saved.Status ?? changes.Status ?? current?.Status,
			Score = saved.Score ?? changes.Score ?? current?.Score,
			WatchedEpisodes = saved.WatchedEpisodes ?? changes.WatchedEpisodes ?? current?.WatchedEpisodes,
			StartDate = saved.StartDate ?? changes.StartDate ?? current?.StartDate,
			FinishDate = saved.FinishDate ?? changes.FinishDate ?? current?.FinishDate,
			UpdatedAt = saved.UpdatedAt ?? DateTime.Now,
		};

		anime.Tracking = tracking;
		WeakReferenceMessenger.Default.Send(new AnimeTrackingChangedMessage(anime, tracking));

		await SyncOtherListsAsync(main, anime, "update", (tracker, id) => tracker.UpdateAsync(id, changes));
		return tracking;
	}

	/// <summary>Removes the anime from the user's list (and their other lists, if they're kept in sync); returns false if it wasn't on it.</summary>
	public async Task<bool> RemoveAsync(AnimeModel anime)
	{
		var main = GetTracker(anime);
		if (!await main.RemoveAsync(anime.Id))
		{
			return false;
		}

		anime.Tracking = null;
		WeakReferenceMessenger.Default.Send(new AnimeTrackingChangedMessage(anime, null));

		await SyncOtherListsAsync(main, anime, "remove it from", (tracker, id) => tracker.RemoveAsync(id));
		return true;
	}

	/// <summary>
	/// Makes a change on each of the user's other lists: the trackers of the databases, other than the anime's own,
	/// that they're signed in to. An anime that database hasn't is left out there.
	/// </summary>
	/// <param name="action">What it did, for the message if it fails, e.g. "update".</param>
	private async Task SyncOtherListsAsync(IAnimeTracker main, AnimeModel anime, string action, Func<IAnimeTracker, long, Task> change)
	{
		if (!preferences.SyncOtherLists)
		{
			return;
		}

		foreach (var tracker in trackers.CreateAll().Where(x => x.Id != main.Id && IsSignedIn(x.Id)))
		{
			try
			{
				// Trackers are named after the database whose ids they take (an AnimeId property).
				if (await anime.FindIdAsync(tracker.Name) is not { } id)
				{
					logger.LogInformation("{Anime} isn't on {List}; it's left out there", anime.Title, tracker.Name);
					continue;
				}

				await change(tracker, id);
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Couldn't {Action} {Anime} on {List}", action, anime.Title, tracker.Name);
				var list = accounts.FirstOrDefault(x => x.Id == tracker.Id)?.Name ?? tracker.Name;
				WeakReferenceMessenger.Default.Send(new OtherListSyncFailedMessage(list, $"Couldn't {action} {anime.Title} on {list}: {ex.Message}"));
			}
		}
	}

	/// <summary>
	/// The account of the selected database if the user isn't signed in to it, so has no list there (its tracker returns an
	/// empty one); null if they are, or there's no database.
	/// </summary>
	public IAccount? GetSignedOutListAccount()
	{
		return trackers.CreateDefault() is { } tracker ? accounts.FirstOrDefault(x => x.Id == tracker.Id && !x.IsSignedIn) : null;
	}

	private bool IsSignedIn(Guid moduleId) => accounts.Any(x => x.Id == moduleId && x.IsSignedIn);

	/// <summary>The tracker of the database the anime came from; its id means nothing to the others.</summary>
	private IAnimeTracker GetTracker(AnimeModel anime)
	{
		return (anime.ServiceId is { } serviceId ? trackers.Create(serviceId) : null) ?? trackers.CreateDefault()
			?? throw new InvalidOperationException("There's no anime database to save to.");
	}
}

/// <summary>
/// Sent (with WeakReferenceMessenger.Default) after an anime's list entry changes, e.g. so the user's list adds, moves
/// or removes its card. <paramref name="Tracking"/> is null if it was removed from the list.
/// </summary>
public sealed record AnimeTrackingChangedMessage(AnimeModel Anime, Tracking? Tracking);

/// <summary>
/// Sent (with WeakReferenceMessenger.Default) when a change to the user's list couldn't be made on one of their other
/// lists (see <see cref="AnimeDatabasePreferences.SyncOtherLists"/>); the change on their main list was made.
/// </summary>
/// <param name="List">The other list's service, e.g. "MyAnimeList".</param>
public sealed record OtherListSyncFailedMessage(string List, string Message);
