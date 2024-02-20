using System.Reflection;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.Plugins.MediaDetection.MpcHc;

public class Plugin : Plugin<INativeMediaPlayer, Config>
{
    public override INativeMediaPlayer Create() => new MediaPlayer();

    public override PluginInfo GetInfo()
    {
        return new PluginInfo
        {
            Name = "mpc-hc64",
            DisplayName = "MPC-HC",
            Description = "",
            Version = Assembly.GetExecutingAssembly().GetName().Version!,
            Icon = typeof(Plugin).Assembly.GetManifestResourceStream("Totoro.Plugins.MediaDetection.MpcHc.mpc-hc-logo.png"),
        };
    }
}
