using MalApi;
using Totoro.Core.Module.Anime;

namespace Totoro.Modules.AnimeDatabase.MyAnimeList;

internal class MyAnimeListTracker(MyAnimeListSession session) : IAnimeTracker
{
	// MAL's maximum page size for a user's list.
	private const int PageSize = 1000;

	public Guid Id => MyAnimeListModule.Id;

	public string Name => nameof(AnimeId.MyAnimeList);

	public async Task<List<AnimeModel>> GetUserList(CancellationToken ct)
	{
		if (!session.IsSignedIn)
		{
			return [];
		}

		var client = await session.GetClientAsync();
		var request = client.Anime()
							.OfUser()
							.WithFields(MalToModelConverter.ListFields)
							.WithLimit(PageSize);

		if (session.IncludeNsfw)
		{
			request.IncludeNsfw();
		}

		var result = await request.Find();
		var response = result.Data.Select(MalToModelConverter.ConvertModel).ToList();

		while (!string.IsNullOrEmpty(result.Paging?.Next))
		{
			ct.ThrowIfCancellationRequested();
			result = await client.GetNextAnimePage(result);
			response.AddRange(result.Data.Select(MalToModelConverter.ConvertModel));
		}

		return response;
	}

	public async Task<bool> Remove(long id)
	{
		var client = await session.GetClientAsync();
		return await client.Anime().WithId(id).RemoveFromList();
	}

	/// <summary>Saves the tracking. Throws if MAL rejects it, so callers don't show a change that didn't happen.</summary>
	public async Task<Tracking> Update(long id, Tracking tracking)
	{
		var client = await session.GetClientAsync();
		var request = client.Anime().WithId(id).UpdateStatus().WithTags("Totoro");

		if (tracking.WatchedEpisodes is { } episodes)
		{
			request.WithEpisodesWatched(episodes);
		}

		switch (tracking.Status)
		{
			// MAL has no rewatching status: it's a flag on an entry, which keeps its status.
			case ListItemStatus.Rewatching:
				request.WithIsRewatching(true);
				break;
			case { } status:
				request.WithStatus(MalToModelConverter.ConvertStatus(status)).WithIsRewatching(false);
				break;
		}

		if (tracking.Score is { } score)
		{
			request.WithScore((Score)Math.Clamp(score, 0, 10));
		}

		if (tracking.StartDate is { } startDate)
		{
			request.WithStartDate(startDate);
		}

		if (tracking.FinishDate is { } finishDate)
		{
			request.WithFinishDate(finishDate);
		}

		return MalToModelConverter.ConvertTracking(await request.Publish()) ?? tracking;
	}
}
