using System.Reflection;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Torrents.Contracts;

namespace Totoro.Plugins.Torrents.Nya;

public class Plugin : Plugin<ITorrentTracker, Config>
{
    public override ITorrentTracker Create() => new Tracker();

    public override PluginInfo GetInfo() => new()
    {
        Name = "nya",
        DisplayName = "Nya",
        Version = Assembly.GetExecutingAssembly().GetName().Version!,
        Description = "A BitTorrent community focused on Eastern Asian media including anime, manga, music, and more",
        Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.Torrents.Nya.nya-logo.png")
    };
}
