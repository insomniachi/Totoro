namespace Totoro.Plugins.Anime.Contracts;

public class AnimeProvider
{
    required public IAnimeStreamProvider StreamProvider { get; init; }
    required public IAnimeCatalog Catalog { get; init; }
    public IAiredAnimeEpisodeProvider? AiredAnimeEpisodeProvider { get; init; }
    public IIdMapper? IdMapper { get; init; }
}
