using System.Reflection;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.Win11MediaPlayer;

public class Plugin : IPlugin<INativeMediaPlayer>
{
    public INativeMediaPlayer Create() => new MediaPlayer();
    public PluginInfo GetInfo()
    {
        return new PluginInfo
        {
            Name = "Microsoft.Media.Player",
            DisplayName = "Windows Media Player",
            Description = "Default Windows 11 Media Player",
            Version = Assembly.GetExecutingAssembly().GetName().Version!
        };
    }

    public PluginOptions GetCurrentConfig() => new();

    public void SetConfig(PluginOptions options) { }

    object IPlugin.Create() => Create();

    public PluginOptions GetDefaultConfig() => new();
}
