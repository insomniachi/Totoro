using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins.Anime.AnimeSaturn;

public class Plugin : Plugin<AnimeProvider, Config>
{
    public override AnimeProvider Create() => new()
    {
        Catalog = new Catalog(),
        StreamProvider = new StreamProvider(),
        AiredAnimeEpisodeProvider = new AiredEpisodesProvider(),
        IdMapper = new IdMapper(),
    };

    public override PluginInfo GetInfo() => new()
    {
        DisplayName = "AnimeSaturn",
        Name = "anime-saturn",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.AnimeSaturn.anime-saturn-logo.png"),
        Description = "Italian provider"
    };
}