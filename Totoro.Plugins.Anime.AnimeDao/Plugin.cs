using System.Reflection;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AnimeDao;

public class Plugin : IPlugin<AnimeProvider>
{
    public AnimeProvider Create() => new()
    {
        Catalog = new Catalog(),
        StreamProvider = new StreamProvider(),
    };

    public PluginInfo GetInfo() => new()
    {
        DisplayName = "Anime Dao",
        Name = "anime-dao",
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Anime.AnimeDao.anime-dao-icon.png"),
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
    };

    public PluginOptions GetOptions()
    {
        return new PluginOptions()
            .AddOption(x => x.WithName(nameof(Config.Url))
                             .WithDisplayName("Url")
                             .WithDescription("Url to home page")
                             .WithValue(Config.Url)
                             .WithGlyph("\uE71B")
                             .ToPluginOption());
    }

    public void SetOptions(PluginOptions options)
    {
        Config.Url = options.GetString(nameof(Config.Url), Config.Url);
    }

    object IPlugin.Create() => Create();
}
