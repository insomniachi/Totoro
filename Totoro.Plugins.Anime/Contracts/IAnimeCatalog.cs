namespace Totoro.Plugins.Anime.Contracts;

public interface IAnimeCatalog
{
    IAsyncEnumerable<ICatalogItem> Search(string query);
}

public interface ICatalogItem
{
    public string Title { get; }
    public string Url { get; }
}