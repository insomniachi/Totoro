using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins.Anime.AnimePahe;

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
        DisplayName = "Anime Pahe",
        Name = "anime-pahe",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.AnimePahe.anime-pahe-logo.png"),
        Description = "AnimePahe is an encode \"group\", was founded in July 2014. encodes on-going anime, completed anime and anime movie."
    };
}