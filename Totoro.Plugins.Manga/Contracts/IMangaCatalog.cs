namespace Totoro.Plugins.Manga.Contracts;

public interface IMangaCatalog
{
    IAsyncEnumerable<ICatalogItem> Search(string query);
}

public interface ICatalogItem
{
    public string Title { get; }
    public string Url { get; }
    public string Image { get; }
}


public interface IChapterProvider
{
    IAsyncEnumerable<ChapterModel> GetChapters(string url);
    IAsyncEnumerable<string> GetImages(ChapterModel chapterModel);
}

#nullable disable
public class ChapterModel
{
    public string Id { get; set; }
    public float Chapter { get; set; }
    public float Volume { get; set; }
}
