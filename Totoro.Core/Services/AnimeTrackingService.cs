using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Services;

/// <summary>
/// Changes the user's list, for the UI and agent tools alike: saves to the tracker of the database the anime came from,
/// updates the anime, and sends an <see cref="AnimeTrackingChangedMessage"/> so every view showing it follows.
/// </summary>
public sealed class AnimeTrackingService(IFactory<IAnimeTracker, Guid> trackers)
{
	/// <summary>
	/// Saves <paramref name="changes"/> to the anime's list entry (adding it to the list if it isn't on it). Only the
	/// fields set in it change; returns the whole entry as it is now.
	/// </summary>
	public async Task<Tracking> UpdateAsync(AnimeModel anime, Tracking changes)
	{
		var saved = await GetTracker(anime).UpdateAsync(anime.Id, changes);

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
		return tracking;
	}

	/// <summary>Removes the anime from the user's list; returns false if it wasn't on it.</summary>
	public async Task<bool> RemoveAsync(AnimeModel anime)
	{
		if (!await GetTracker(anime).RemoveAsync(anime.Id))
		{
			return false;
		}

		anime.Tracking = null;
		WeakReferenceMessenger.Default.Send(new AnimeTrackingChangedMessage(anime, null));
		return true;
	}

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
