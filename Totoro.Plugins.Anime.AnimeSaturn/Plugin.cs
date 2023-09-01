using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AnimeSaturn;

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
        DisplayName = "AnimeSaturn",
        Name = "anime-saturn",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.AnimeSaturn.anime-saturn-logo.png"),
        Description = "Italian provider"
    };

    public PluginOptions GetOptions()
    {
        return new PluginOptions()
            .AddOption(x =>
            {
               return x.WithNameAndValue(Config.Url)
                       .WithDisplayName("Url")
                       .WithDescription("Url to home page")
                       .WithGlyph(Glyphs.Url)
                       .ToPluginOption();
            });
    }

    public void SetOptions(PluginOptions options)
    {
        Config.Url = options.GetString(nameof(Config.Url), Config.Url);
    }

    object IPlugin.Create() => Create();
}