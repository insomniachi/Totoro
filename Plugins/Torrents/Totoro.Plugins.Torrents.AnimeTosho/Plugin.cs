using System.Reflection;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Torrents.Contracts;

namespace Totoro.Plugins.Torrents.AnimeTosho;

public class Plugin : Plugin<ITorrentTracker, Config>
{
    public override ITorrentTracker Create() => new Tracker();

    public override PluginInfo GetInfo() => new()
    {
        Name = "anime-tosho",
        DisplayName = "Anime Tosho",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Description = "Anime Tosho is a free, completely automated service which mirrors most torrents posted on TokyoTosho, Nyaa and AniDex",
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Torrents.AnimeTosho.anime-tosho-icon.ico")
    };
}
