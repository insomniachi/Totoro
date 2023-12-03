using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AnimePahe;

public class Plugin : IPlugin<AnimeProvider>
{
    public AnimeProvider Create() => new()
    {
        Catalog = new Catalog(),
        StreamProvider = new StreamProvider(),
        AiredAnimeEpisodeProvider = new AiredEpisodesProvider(),
        IdMapper = new IdMapper(),
    };

    public PluginInfo GetInfo() => new()
    {
        DisplayName = "Anime Pahe",
        Name = "anime-pahe",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.AnimePahe.anime-pahe-logo.png"),
        Description = "AnimePahe is an encode \"group\", was founded in July 2014. encodes on-going anime, completed anime and anime movie."
    };

    public PluginOptions GetOptions()
    {
        return new PluginOptions()
            .AddOption(x =>
            {
                return x.WithName(nameof(Config.Url))
                        .WithDisplayName("Url")
                        .WithDescription("Url to home page")
                        .WithValue(Config.Url)
                        .WithGlyph("\uE71B")
                        .ToPluginOption();
            });
    }

    public void SetOptions(PluginOptions options)
    {
        Config.Url = options.GetString(nameof(Config.Url), Config.Url);
    }

    object IPlugin.Create() => Create();
}