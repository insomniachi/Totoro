using Totoro.Plugins.Manga.Contracts;

namespace Totoro.Plugins.Manga;

public class MangaProvider
{
    required public IMangaCatalog Catalog { get; init; }
    required public IChapterProvider ChapterProvider { get; init; }
}
