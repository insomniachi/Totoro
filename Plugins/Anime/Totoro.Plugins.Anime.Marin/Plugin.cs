using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Marin;

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
        DisplayName = "Marin",
        Name = "marin",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.Marin.marin-logo.jpg"),
        Description = "Formerly tenshi.moe"
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
