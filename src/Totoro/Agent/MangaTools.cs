using System.ComponentModel;
using Totoro.Commands;
using Totoro.Core.Agent;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;
using Totoro.Core.Services;
using Totoro.ViewModels.Manga;

namespace Totoro.Agent;

/// <param name="Status">Where it is on the user's list, e.g. "Reading"; "Not on list" if it isn't.</param>
public sealed record MangaSummary(long Id, string Title, string Format, int? TotalChapters, int? ChaptersRead, string Status, float? MeanScoreOutOf10)
{
	public static MangaSummary From(MangaModel manga) => new(manga.Id, manga.Title, manga.Format.ToString(), manga.TotalChapters,
		manga.Tracking?.ChaptersRead, MediaText.Status(manga.Tracking?.Status, Core.Navigation.MediaMode.Manga), manga.MeanScore / 10);
}

/// <summary>Agent tools for manga: finding them in the manga database, the user's manga list, and reading.</summary>
public sealed class MangaTools(
	IFactory<IMangaMetadataProvider, Guid> databases,
	IFactory<IMangaTracker, Guid> trackers,
	MangaTrackingService tracking,
	MangaReadingService reading)
{
	[AgentTool("search_manga")]
	[Description("Searches the manga database by title; best match first.")]
	public async Task<IReadOnlyList<MangaSummary>> SearchMangaAsync(
		[Description("Words from its title, as the user wrote them.")] string title,
		[Description("How many results to return, at most.")] int limit = 10)
	{
		var database = databases.CreateDefault() ?? throw new AgentToolException("There's no manga database; the user picks one in Settings.");
		return [.. (await database.SearchMangaAsync(title, CancellationToken.None)).Take(limit).Select(MangaSummary.From)];
	}

	[AgentTool("get_manga_list")]
	[Description("The user's manga list, or the part of it with one status.")]
	public async Task<IReadOnlyList<MangaSummary>> GetMangaListAsync([Description("Only those with this status; Watching means reading.")] ListItemStatus? status = null)
	{
		var tracker = trackers.CreateDefault() ?? throw new AgentToolException("There's no manga database; the user picks one in Settings.");
		var list = await tracker.GetUserListAsync(CancellationToken.None);
		return [.. list.Where(x => status is null || x.Tracking?.Status == status).Select(MangaSummary.From)];
	}

	[AgentTool("update_manga_tracking")]
	[Description("Changes a manga's entry on the user's list (adding it if it isn't on it). Only the values given change.")]
	public async Task<MangaSummary> UpdateMangaTrackingAsync(
		[Description("Its id, from search_manga or get_manga_list.")] long mangaId,
		[Description("Watching means reading, Rewatching rereading, PlanToWatch planning.")] ListItemStatus? status = null,
		int? chaptersRead = null,
		int? volumesRead = null,
		[Description("Out of 10.")] double? score = null)
	{
		var manga = await GetAsync(mangaId);
		await tracking.UpdateAsync(manga, new MangaTracking { Status = status, ChaptersRead = chaptersRead, VolumesRead = volumesRead, Score = score });
		return MangaSummary.From(manga);
	}

	[AgentTool("remove_manga_from_list", RequiresApproval = true)]
	[Description("Removes a manga from the user's list, with their progress and score on it.")]
	public async Task<string> RemoveMangaAsync(long mangaId)
	{
		var manga = await GetAsync(mangaId);
		return await tracking.RemoveAsync(manga) ? $"Removed {manga.Title} from the list." : $"{manga.Title} wasn't on the list.";
	}

	[AgentTool("read_manga")]
	[Description("Opens the reader in the app at the next chapter of a manga to read (after those read), from the manga source.")]
	public async Task<string> ReadMangaAsync(long mangaId, [Description("A particular chapter to open instead.")] float? chapter = null)
	{
		var manga = await GetAsync(mangaId);
		var source = reading.Source ?? throw new AgentToolException("No manga source is installed; the user gets one from the Plugins page.");
		var match = await reading.FindAsync(source, manga, CancellationToken.None)
			?? throw new AgentToolException($"{source.Name} doesn't seem to have {manga.Title}. The user can pick it by hand in its details.");
		var chapters = await MangaReadingService.GetChaptersAsync(source, match.Id, CancellationToken.None);
		var open = (chapter is { } number ? chapters.FirstOrDefault(x => x.Number == number) : MangaReadingService.NextChapter(chapters, manga.Tracking))
			?? throw new AgentToolException(chapters.Count == 0 ? $"{source.Name} has no chapters of {manga.Title}." : $"{source.Name} has no chapter {chapter}.");
		await MangaCommands.OpenReaderAsync(new ReaderRequest(manga, source.Id, chapters, chapters.IndexOf(open)));
		return $"Opened chapter {open.Number} of {manga.Title}.";
	}

	private async Task<MangaModel> GetAsync(long id)
	{
		var database = databases.CreateDefault() ?? throw new AgentToolException("There's no manga database; the user picks one in Settings.");
		try
		{
			return await database.GetMangaAsync(id);
		}
		catch (Exception ex)
		{
			throw new AgentToolException($"There's no manga {id} in {database.Name}: search for it to get its id. ({ex.Message})");
		}
	}
}
