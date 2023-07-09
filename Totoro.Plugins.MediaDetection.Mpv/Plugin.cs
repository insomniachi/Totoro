using System.Reflection;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.Generic;

public abstract class GenericPlugin<T> : IPlugin<INativeMediaPlayer>
    where T : INativeMediaPlayer, new()
{
    public INativeMediaPlayer Create() => new T();

    public abstract PluginInfo GetInfo();
    public virtual PluginOptions GetOptions() => new();
    public virtual void SetOptions(PluginOptions options) { }
    object IPlugin.Create() => Create();
}

public class MpvPlugin : GenericPlugin<Mpv>
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

    public override PluginOptions GetOptions() => new PluginOptions()
        .AddOption(x => x.WithNameAndValue(MpvConfig.FileName)
                         .ToPluginOption());

    public override void SetOptions(PluginOptions options)
    {
        MpvConfig.FileName = options.GetString(nameof(MpvConfig.FileName), MpvConfig.FileName);
    }
}

public class MpcHcPlugin : GenericPlugin<MpcHc>
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

    public override PluginOptions GetOptions() => new PluginOptions()
        .AddOption(x => x.WithNameAndValue(MpcConfig.FileName)
                         .ToPluginOption());

    public override void SetOptions(PluginOptions options)
    {
        MpvConfig.FileName = options.GetString(nameof(MpcConfig.FileName), MpvConfig.FileName);
    }
}