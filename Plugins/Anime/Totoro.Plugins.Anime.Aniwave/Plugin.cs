using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Aniwave;

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
        DisplayName = "Aniwave",
        Name = "aniwave",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.Aniwave.aniwave-logo.png"),
        Description = "previously 9anime"
    };
}