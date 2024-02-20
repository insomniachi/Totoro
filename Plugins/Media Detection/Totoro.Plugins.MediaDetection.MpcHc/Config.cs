using System.ComponentModel;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.MpcHc;

public class Config : ConfigObject
{
    [DisplayName("Path")]
    [Description("Path to executable")]
    [Glyph(Glyphs.File)]
    public string FileName { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"MPC-HC\mpc-hc64.exe");

    [Glyph(Glyphs.Website)]
    public string Host { get; set; } = "127.0.0.1";

    [Glyph(Glyphs.Port)]
    [Description("View > Options > Web Interface > Listen on port")]
    public int Port { get; set; } = 13579;
}