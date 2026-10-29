using Totoro.Core.Modules.Streaming;

namespace Totoro.Core.Modules.Manga;

/// <summary>
/// A source of manga to read (e.g. MangaDex): finds a manga, lists its chapters and each chapter's pages. Modules
/// register implementations keyed by their module id.
/// </summary>
public interface IMangaProvider
{
	/// <summary>The module's id.</summary>
	Guid Id { get; }

	string Name { get; }

	/// <summary>Manga whose title matches <paramref name="query"/>, best match first.</summary>
	Task<List<ProviderManga>> SearchAsync(string query, CancellationToken ct);

	/// <summary>
	/// The source's manga that is <paramref name="manga"/> (from a database), or null if it hasn't it. By default, the
	/// best title match of a search; sources that know their manga's database ids should match those first.
	/// </summary>
	Task<ProviderManga?> FindMangaAsync(MangaModel manga, CancellationToken ct) => MatchByTitleAsync(this, manga, ct);

	/// <summary>The best close match of a search of <paramref name="source"/> for each of the manga's titles in turn; null if none is close.</summary>
	static async Task<ProviderManga?> MatchByTitleAsync(IMangaProvider source, MangaModel manga, CancellationToken ct)
	{
		foreach (var title in manga.AllTitles)
		{
			var best = (await source.SearchAsync(title, ct)).MaxBy(x => TitleMatch.Score(title, x.Title));
			if (best is not null && TitleMatch.Score(title, best.Title) >= 0.8)
			{
				return best;
			}
		}

		return null;
	}

	/// <summary>The manga's chapters the source has, by number, one per number.</summary>
	Task<List<ProviderChapter>> GetChaptersAsync(string mangaId, CancellationToken ct);

	/// <summary>The chapter's pages, in reading order.</summary>
	Task<List<MangaPage>> GetPagesAsync(ProviderChapter chapter, CancellationToken ct);
}

/// <param name="Score">How well the title matched the search, from 0 to 1; 1 is an exact match.</param>
public sealed record ProviderManga(string Id, string Title, double Score = 0)
{
	/// <summary>Its cover, a URL; "" when the source has none.</summary>
	public string Image { get; init; } = "";
}

/// <param name="Number">The chapter number, e.g. 12.5 for an extra between 12 and 13.</param>
public sealed record ProviderChapter(string MangaId, string Id, float Number)
{
	/// <summary>Its name, e.g. "Chapter 12: The Return"; "" when it has none.</summary>
	public string Title { get; init; } = "";
	public string? Volume { get; init; }
	public string? Language { get; init; }
}

/// <param name="Url">The page's image.</param>
public sealed record MangaPage(Uri Url)
{
	/// <summary>HTTP headers to send with the request for the image, e.g. a Referer.</summary>
	public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}
