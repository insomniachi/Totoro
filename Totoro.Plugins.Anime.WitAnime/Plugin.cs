using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.WitAnime;

public class Plugin : IPlugin<AnimeProvider>
{
    public AnimeProvider Create() => new()
    {
        Catalog = new Catalog(),
        StreamProvider = new StreamProvider(),
        AiredAnimeEpisodeProvider = new AiredEpisodesProvider(),
    };

    public PluginInfo GetInfo() => new()
    {
        DisplayName = "Wit Anime",
        Name = "wit-anime",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.WitAnime.wit-anime-logo.png"),
        Description = "Arabic provider"
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