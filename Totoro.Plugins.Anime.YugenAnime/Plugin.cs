using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.YugenAnime;

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
        DisplayName = "Yugen Anime",
        Name = "yugen-anime",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.YugenAnime.yugen-anime-icon.png"),
        Description = "A gogo anime scrapper site"
    };

    public PluginOptions GetOptions()
    {
        return new PluginOptions()
            .AddOption(x => x.WithName(nameof(Config.Url))
                             .WithDisplayName("Url")
                             .WithDescription("Url to home page")
                             .WithGlyph("\uE71B")
                             .WithValue(Config.Url)
                             .ToPluginOption())
            .AddOption(x => x.WithName(nameof(Config.StreamType))
                             .WithDisplayName("Default Stream Type")
                             .WithDescription("Choose what to play by default, sub/dub")
                             .WithGlyph("\uF2B7")
                             .WithValue(Config.StreamType)
                             .WithAllowedValues(new[] { StreamType.EnglishSubbed, StreamType.EnglishDubbed })
                             .ToSelectablePluginOption());
    }

    public void SetOptions(PluginOptions options)
    {
        Config.Url = options.GetString(nameof(Config.Url), Config.Url);
        Config.StreamType = options.GetEnum(nameof(Config.StreamType), Config.StreamType);
    }

    object IPlugin.Create() => Create();
}
