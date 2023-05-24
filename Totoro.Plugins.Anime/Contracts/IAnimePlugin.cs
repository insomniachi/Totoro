namespace Totoro.Plugins.Anime.Contracts;

public class AnimePlugin
{
    required public IAnimeStreamProvider StreamProvider { get; init; }
    required public  IAnimeCatalog Catalog { get; init; }
    public IAiredAnimeEpisodeProvider? AiredAnimeEpisodeProvider { get; init; }
}
