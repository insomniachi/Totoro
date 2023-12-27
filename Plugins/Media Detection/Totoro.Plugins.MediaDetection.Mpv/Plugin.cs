using System.Reflection;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.Generic;

public abstract class GenericPlugin<T, TConfig> : Plugin<INativeMediaPlayer, TConfig>
    where T : INativeMediaPlayer, new()
    where TConfig : ConfigObject, new()
{
    public override INativeMediaPlayer Create() => new T();
}

public class MpvPlugin : GenericPlugin<Mpv, MpvConfig>
{
    public override PluginInfo GetInfo()
    {
        return new PluginInfo
        {
            Name = "mpv",
            DisplayName = "MPV",
            Description = "",
            Version = Assembly.GetExecutingAssembly().GetName().Version!
        };
    }
}

public class MpcHcPlugin : GenericPlugin<MpcHc, MpcConfig>
{
    public override PluginInfo GetInfo()
    {
        return new PluginInfo
        {
            Name = "mpc-hc64",
            DisplayName = "MPC-HC",
            Description = "",
            Version = Assembly.GetExecutingAssembly().GetName().Version!
        };
    }
}