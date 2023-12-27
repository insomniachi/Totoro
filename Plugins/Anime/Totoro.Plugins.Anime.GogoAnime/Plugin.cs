using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins.Anime.GogoAnime;

public class Plugin : Plugin<AnimeProvider, Config>
{
    public override AnimeProvider Create() => new()
    {
        Catalog = new Catalog(),
        StreamProvider = new StreamProvider(),
        AiredAnimeEpisodeProvider = new AiredEpisodesProvider()
    };

    public override PluginInfo GetInfo() => new()
    {
        DisplayName = "Gogo Anime",
        Name = "gogo-anime",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.GogoAnime.gogo-anime-logo.png"),
    };
}