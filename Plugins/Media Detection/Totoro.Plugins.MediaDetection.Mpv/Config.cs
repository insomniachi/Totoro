using System.ComponentModel;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.Generic;

public class MpvConfig : ConfigObject
{
    [DisplayName("Path")]
    [Description("Path to executable")]
    [Glyph(Glyphs.File)]
    public string FileName { get; set; } = @"";
}
