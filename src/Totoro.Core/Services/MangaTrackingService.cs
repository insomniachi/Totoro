using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;

namespace Totoro.Core.Services;

/// <summary>
/// Changes the user's manga list, for the UI and agent tools alike: saves to the tracker of the database the manga came
/// from, updates the manga, and sends a <see cref="MangaTrackingChangedMessage"/> so every view showing it follows.
/// </summary>
public sealed class MangaTrackingService(IFactory<IMangaTracker, Guid> trackers)
{
	/// <summary>Saves the fields set in <paramref name="changes"/> (adding the manga to the list if it isn't on it); returns the whole entry as it is now.</summary>
	public async Task<MangaTracking> UpdateAsync(MangaModel manga, MangaTracking changes)
	{
		var saved = await GetTracker(manga).UpdateAsync(manga.Id, changes);

		// Trackers return what they changed; the rest stays as it was.
		var current = manga.Tracking;
		var tracking = new MangaTracking
		{
			Status = saved.Status ?? changes.Status ?? current?.Status,
			Score = saved.Score ?? changes.Score ?? current?.Score,
			ChaptersRead = saved.ChaptersRead ?? changes.ChaptersRead ?? current?.ChaptersRead,
			VolumesRead = saved.VolumesRead ?? changes.VolumesRead ?? current?.VolumesRead,
			StartDate = saved.StartDate ?? changes.StartDate ?? current?.StartDate,
			FinishDate = saved.FinishDate ?? changes.FinishDate ?? current?.FinishDate,
			UpdatedAt = saved.UpdatedAt ?? DateTime.Now,
		};

		manga.Tracking = tracking;
		WeakReferenceMessenger.Default.Send(new MangaTrackingChangedMessage(manga, tracking));
		return tracking;
	}

	/// <summary>Removes the manga from the user's list; false if it wasn't on it.</summary>
	public async Task<bool> RemoveAsync(MangaModel manga)
	{
		if (!await GetTracker(manga).RemoveAsync(manga.Id))
		{
			return false;
		}

		manga.Tracking = null;
		WeakReferenceMessenger.Default.Send(new MangaTrackingChangedMessage(manga, null));
		return true;
	}

	/// <summary>
	/// What finishing chapter <paramref name="chapter"/> changes on the list, or null for nothing: progress only moves
	/// forward (rereading an earlier chapter changes nothing), and reaching the last chapter completes it.
	/// </summary>
	/// <param name="volumesRead">The volumes read once it's read (see <see cref="VolumesRead"/>), if known; set only if more than before.</param>
	public static MangaTracking? ChangesForChapterRead(MangaModel manga, float chapter, int? volumesRead = null)
	{
		var read = (int)chapter;
		if (read <= (manga.Tracking?.ChaptersRead ?? 0))
		{
			return null;
		}

		var completed = manga.TotalChapters is { } total && read >= total;
		return new MangaTracking
		{
			ChaptersRead = read,
			VolumesRead = volumesRead > (manga.Tracking?.VolumesRead ?? 0) ? volumesRead : null,
			Status = completed ? ListItemStatus.Completed
				: manga.Tracking?.Status == ListItemStatus.Rewatching ? ListItemStatus.Rewatching : ListItemStatus.Watching,
			StartDate = manga.Tracking?.StartDate is null ? DateTime.Today : null,
			FinishDate = completed ? DateTime.Today : null,
		};
	}

	/// <summary>
	/// The volumes read once chapter <paramref name="index"/> of <paramref name="chapters"/> (a source's, in reading order) is:
	/// all of its volume if it's that volume's last chapter, else those before it. Null when the source doesn't say which
	/// volume it's in; the databases don't, so only a source that does (MangaDex) gives volumes.
	/// </summary>
	public static int? VolumesRead(MangaModel manga, IReadOnlyList<ProviderChapter> chapters, int index)
	{
		if (!int.TryParse(chapters[index].Volume, out var volume))
		{
			return null;
		}

		// The last chapter of a volume: the next is in a later one, or there's none and no more are coming.
		var last = index + 1 < chapters.Count
			? int.TryParse(chapters[index + 1].Volume, out var next) && next > volume
			: manga.Status == AiringStatus.FinishedAiring;
		return last ? volume : volume - 1;
	}

	/// <summary>Records that the user finished reading chapter <paramref name="index"/> of <paramref name="chapters"/>; returns what changed, or null for nothing.</summary>
	public async Task<MangaTracking?> MarkChapterReadAsync(MangaModel manga, IReadOnlyList<ProviderChapter> chapters, int index)
	{
		if (ChangesForChapterRead(manga, chapters[index].Number, VolumesRead(manga, chapters, index)) is not { } changes)
		{
			return null;
		}

		await UpdateAsync(manga, changes);
		return changes;
	}

	/// <summary>The tracker of the database the manga came from; its id means nothing to the others.</summary>
	public IMangaTracker GetTracker(MangaModel manga)
	{
		return (manga.ServiceId is { } serviceId ? trackers.Create(serviceId) : null) ?? trackers.CreateDefault()
			?? throw new InvalidOperationException("There's no manga database to save to.");
	}
}

/// <summary>Sent (with WeakReferenceMessenger.Default) after a manga's list entry changes; <paramref name="Tracking"/> is null if it was removed.</summary>
public sealed record MangaTrackingChangedMessage(MangaModel Manga, MangaTracking? Tracking);
