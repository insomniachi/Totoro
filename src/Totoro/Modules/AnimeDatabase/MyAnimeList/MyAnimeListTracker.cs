using MalApi;
using Totoro.Core.Modules.Anime;

namespace Totoro.Modules.AnimeDatabase.MyAnimeList;

internal class MyAnimeListTracker(MyAnimeListSession session) : IAnimeTracker
{
	// MAL's maximum page size for a user's list.
	private const int PageSize = 1000;

	// In the order of MAL's enums (Priority, Value), so a choice's index is its value.
	private static readonly string[] s_priorities = ["Low", "Medium", "High"];
	private static readonly string[] s_rewatchValues = ["Very low", "Low", "Medium", "High", "Very high"];

	public Guid Id => MyAnimeListModule.Id;

	public string Name => nameof(AnimeId.MyAnimeList);

	public async Task<List<AnimeModel>> GetUserListAsync(CancellationToken ct)
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

	public async Task<bool> RemoveAsync(long id)
	{
		var client = await session.GetClientAsync();
		return await client.Anime().WithId(id).RemoveFromList();
	}

	public async Task<TrackingForm> GetFormAsync(long id, CancellationToken ct)
	{
		var client = await session.GetClientAsync();
		// MAL leaves these out of the list status unless they're asked for by name.
		var anime = await client.Anime().WithId(id)
								.WithFields($"{AnimeFieldNames.UserStatus}{{priority,num_times_rewatched,rewatch_value,tags,comments}}")
								.Find();
		var status = anime.UserStatus;

		return new TrackingForm(10, 1,
		[
			new TextField("tags", "Tags (comma separated)", string.Join(", ", status?.Tags ?? [])),
			new ChoiceField("priority", "Priority", s_priorities, s_priorities[(int)(status?.Priority ?? Priority.Low)]),
			new NumberField("rewatches", "Total times rewatched", status?.RewatchCount ?? 0),
			new ChoiceField("rewatchValue", "Rewatch value", s_rewatchValues, s_rewatchValues[(int)(status?.RewatchValue ?? Value.VeryLow)]),
			new TextField("notes", "Notes", status?.Comments) { IsMultiline = true },
		]);
	}

	/// <summary>Saves the tracking. Throws if MAL rejects it, so callers don't show a change that didn't happen.</summary>
	public async Task<Tracking> UpdateAsync(long id, Tracking tracking)
	{
		var client = await session.GetClientAsync();
		var request = client.Anime().WithId(id).UpdateStatus();

		if (tracking.Changed<TextField>("tags") is { } tags)
		{
			request.WithTags(tags.Value);
		}

		if (tracking.Changed<ChoiceField>("priority") is { } priority)
		{
			request.WithPriority((Priority)priority.Index);
		}

		if (tracking.Changed<NumberField>("rewatches") is { } rewatches)
		{
			request.WithRewatchCount((int)rewatches.Value);
		}

		if (tracking.Changed<ChoiceField>("rewatchValue") is { } rewatchValue)
		{
			request.WithRewatchValue((Value)rewatchValue.Index);
		}

		if (tracking.Changed<TextField>("notes") is { } notes)
		{
			request.WithComments(notes.Value);
		}

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
			request.WithScore((Score)Math.Clamp((int)Math.Round(score), 0, 10));
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
