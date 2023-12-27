using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AllAnime;

[ExcludeFromCodeCoverage]
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
        DisplayName = "AllAnime",
        Name = "allanime",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.AllAnime.allanime-icon.png"),
        Description = "AllAnime's goal is to provide you with the highest possible amount of daily anime episodes/manga chapters for free and without any kind of limitation."
    };

    private static readonly string[] _allowedValues = ["ALL", "JP", "CN", "KR"];

    public PluginOptions GetOptions()
    {
        return new PluginOptions()
            .AddOption(x => x.WithName(nameof(Config.Url))
                             .WithDisplayName("Url")
                             .WithDescription("Url to home page")
                             .WithValue(Config.Url)
                             .WithGlyph("\uE71B")
                             .ToPluginOption())
            .AddOption(x => x.WithName(nameof(Config.StreamType))
                             .WithDisplayName("Stream Type")
                             .WithDescription("Choose what to play by default, sub/dub")
                             .WithGlyph("\uF2B7")
                             .WithValue(Config.StreamType)
                             .WithAllowedValues(new[] { StreamType.Subbed(Languages.English), StreamType.Dubbed(Languages.English), StreamType.Raw() })
                             .ToSelectablePluginOption())
            .AddOption(x => x.WithName(nameof(Config.CountryOfOrigin))
                             .WithDisplayName("Country Of Origin")
                             .WithDescription("Filter anime by country")
                             .WithGlyph("\uE909")
                             .WithValue(Config.CountryOfOrigin)
                             .WithAllowedValues(_allowedValues)
                             .ToSelectablePluginOption());
    }

    public void SetOptions(PluginOptions options)
    {
        Config.Url = options.GetString(nameof(Config.Url), Config.Url);
        Config.StreamType = options.GetRecord(nameof(Config.StreamType), Config.StreamType);
        Config.CountryOfOrigin = options.GetString(nameof(Config.CountryOfOrigin), Config.CountryOfOrigin);
    }

    object IPlugin.Create() => Create();
}
