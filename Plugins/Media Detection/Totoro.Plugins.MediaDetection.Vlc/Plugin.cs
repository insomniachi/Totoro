using System.Reflection;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.Plugins.MediaDetection.Vlc;

public class Plugin : Plugin<INativeMediaPlayer, Config>
{
    public override INativeMediaPlayer Create() => new MediaPlayer();

    public override PluginInfo GetInfo()
    {
        return new PluginInfo
        {
            Name = "vlc",
            DisplayName = "VLC Media Player",
            Description = "",
            Version = Assembly.GetExecutingAssembly().GetName().Version!,
            Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.MediaDetection.Vlc.vlc_logo.png"),
        };
    }
}
