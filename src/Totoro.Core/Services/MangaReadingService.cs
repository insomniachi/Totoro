using Totoro.Core.Modules;
using Totoro.Core.Modules.Manga;
using Totoro.Core.Settings;

namespace Totoro.Core.Services;

/// <summary>
/// Finds a database's manga on the selected source and lists its chapters, for the UI and agent tools alike. A match
/// the user picked by hand (the automatic one was wrong) is kept, per source and manga.
/// </summary>
public sealed class MangaReadingService(IFactory<IMangaProvider, Guid> providers, IPreferences preferences)
{
	private const string MatchesKey = "MangaSourceMatches";

	/// <summary>The source chapters are read from; null when none is installed (they come as plugins).</summary>
	public IMangaProvider? Source => providers.CreateDefault();

	public IMangaProvider? GetSource(Guid id) => providers.Create(id);

	public IEnumerable<IMangaProvider> Sources => providers.CreateAll().DistinctBy(x => x.Id);

	/// <summary>The source's manga that is <paramref name="manga"/>: the one the user picked, else the source's own match; null if it hasn't it.</summary>
	public async Task<ProviderManga?> FindAsync(IMangaProvider source, MangaModel manga, CancellationToken ct)
	{
		return GetMatches().TryGetValue(MatchKey(source, manga), out var picked) ? picked : await source.FindMangaAsync(manga, ct);
	}

	/// <summary>Remembers that <paramref name="match"/> is <paramref name="manga"/> on the source, e.g. after the automatic match was wrong.</summary>
	public void SaveMatch(IMangaProvider source, MangaModel manga, ProviderManga match)
	{
		var matches = GetMatches();
		matches[MatchKey(source, manga)] = match;
		preferences.SaveKey(MatchesKey, matches);
	}

	/// <summary>The manga's chapters, in reading order.</summary>
	public static async Task<List<ProviderChapter>> GetChaptersAsync(IMangaProvider source, string mangaId, CancellationToken ct)
	{
		return [.. (await source.GetChaptersAsync(mangaId, ct)).OrderBy(x => x.Number)];
	}

	/// <summary>The chapter to read next: the first after those read, else the first; null if there are none.</summary>
	public static ProviderChapter? NextChapter(IReadOnlyList<ProviderChapter> chapters, MangaTracking? tracking)
	{
		var read = tracking?.ChaptersRead ?? 0;
		return chapters.FirstOrDefault(x => x.Number > read) ?? chapters.FirstOrDefault();
	}

	private Dictionary<string, ProviderManga> GetMatches() => preferences.ReadKey<Dictionary<string, ProviderManga>>(MatchesKey) ?? [];

	private static string MatchKey(IMangaProvider source, MangaModel manga) => $"{source.Id}/{manga.ServiceId}/{manga.Id}";
}
