using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.GogoAnime;

public class Plugin : IPlugin<AnimeProvider>
{
    public AnimeProvider Create() => new()
    {
        Catalog = new Catalog(),
        StreamProvider = new StreamProvider(),
        AiredAnimeEpisodeProvider = new AiredEpisodesProvider()
    };

    public PluginInfo GetInfo() => new()
    {
        DisplayName = "Gogo Anime",
        Name = "gogo-anime",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.GogoAnime.gogo-anime-logo.png"),
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