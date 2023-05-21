namespace Totoro.Plugins.Anime.Contracts;

public interface IAnimePlugin
{
    IAnimeStreamProvider StreamProvider { get; }
    IAnimeCatalog Catalog { get; }
}
