using System.Reflection;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.Plugins.Options;

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
            Version = Assembly.GetExecutingAssembly().GetName().Version!
        };
    }
}

public class Config : ConfigObject
{
    public string FileName { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VideoLAN\VLC\vlc.exe");
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;
    public string Password { get; set; } = "password";
}