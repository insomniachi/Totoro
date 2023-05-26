using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Zoro;

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
        DisplayName = "Zoro",
        Name = "zoro",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.Zoro.zoro-logo.png"),
        Description = "Zoro is a free site to watch anime and you can even download subbed or dubbed anime in ultra HD quality without any registration or payment."
    };

    public PluginOptions GetOptions()
    {
        return new PluginOptions()
            .AddOption(x => x.WithName(nameof(Config.Url))
                             .WithDisplayName("Url")
                             .WithDescription("Url to home page")
                             .WithGlyph("\uE71B")
                             .WithValue(Config.Url)
                             .ToPluginOption());
    }

    public void SetOptions(PluginOptions options)
    {
        Config.Url = options.GetString(nameof(Config.Url), Config.Url);
    }

    object IPlugin.Create() => Create();
}
