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

    public PluginOptions GetOptions() => new PluginOptions()
        .AddOption(x => x.WithNameAndValue(Config.FileName)
                         .ToPluginOption());

    public void SetOptions(PluginOptions options) 
    {
        Config.FileName = options.GetString(nameof(Config.FileName), Config.FileName);
    }

    object IPlugin.Create() => Create();
}

public static class Config
{
    public static string FileName { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VideoLAN\VLC\vlc.exe");
}
