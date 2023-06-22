using System.Reflection;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.Vlc;

public class Plugin : IPlugin<INativeMediaPlayer>
{
    public INativeMediaPlayer Create() => new MediaPlayer();

    public PluginInfo GetInfo()
    {
        return new PluginInfo
        {
            Name = "vlc",
            DisplayName = "VLC Media Player",
            Description = "",
            Version = Assembly.GetExecutingAssembly().GetName().Version!
        };
    }

    public PluginOptions GetOptions() => new();

    public void SetOptions(PluginOptions options) { }

    object IPlugin.Create() => Create();
}
