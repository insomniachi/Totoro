using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AllAnime;

public class Plugin : IPlugin<AnimePlugin>
{
    public AnimePlugin Create() => new()
    {
        Catalog = new Catalog(),
        StreamProvider = new StreamProvider(),
        AiredAnimeEpisodeProvider = new AiredEpisodesProvider()
    };

    public PluginInfo GetInfo() => new()
    {
        DisplayName = "Allanime",
        Name = "allanime",
        Version = Assembly.GetExecutingAssembly().GetName().Version!
    };

    public PluginOptions GetOptions()
    {
        return new PluginOptions()
            .AddOption(nameof(Config.Url), "Url", Config.Url)
            .AddSelectableOption(nameof(Config.StreamType), "Stream Type", Config.StreamType, new[] { StreamType.EnglishSubbed, StreamType.EnglishDubbed, StreamType.Raw })
            .AddSelectableOption(nameof(Config.CountryOfOrigin), "Country of Origin", Config.CountryOfOrigin, new[] { "ALL", "JP", "CN", "KR" });
    }

    public void SetOptions(PluginOptions options)
    {
        Config.Url = options.GetString(nameof(Config.Url), Config.Url);
        Config.StreamType = options.GetEnum(nameof(Config.StreamType), Config.StreamType);
    }

    object IPlugin.Create() => Create();
}
