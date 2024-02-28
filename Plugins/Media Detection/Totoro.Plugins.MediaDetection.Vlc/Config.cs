using System.ComponentModel;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.Vlc;

public class Config : ConfigObject
{
    [DisplayName("Path")]
    [Description("Path to executable")]
    [Glyph(Glyphs.File)]
    public string FileName { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VideoLAN\VLC\vlc.exe");

    [Glyph(Glyphs.Website)]
    [Description("Enable web interface Preferences > Interface > Main Interfaces > Web Interface")]
    public string Host { get; set; } = "127.0.0.1";

    [Glyph(Glyphs.Port)]
    public int Port { get; set; } = 8080;

    [Glyph(Glyphs.Password)]
    [Description("Password set in Preferences > Interface > Main Interfaces > Lua > Lua HTTP password")]
    public string Password { get; set; } = "";
}