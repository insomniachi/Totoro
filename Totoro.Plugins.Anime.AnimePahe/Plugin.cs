using System.Reflection;
using Plugin.AnimePahe;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AnimePahe;

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
        DisplayName = "Anime Pahe",
        Name = "anime-pahe",
        Version = Assembly.GetExecutingAssembly().GetName().Version!
    };

    public PluginOptions GetOptions()
    {
        return new PluginOptions().AddOption(nameof(Config.Url), "Url", Config.Url);
    }

    public void SetOptions(PluginOptions options)
    {
        Config.Url = options.GetString(nameof(Config.Url), Config.Url);
    }

    object IPlugin.Create() => Create();
}