using System.ComponentModel;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Torrents.AnimeTosho;

public class Config : ConfigObject
{
    [Description("Url to home page")]
    [Glyph(Glyphs.Url)]
    public string Url { get; set; } = @"https://animetosho.org/";

    [Glyph(Glyphs.Filter)]
    public Filter Filter { get; set; } = Filter.TrustedOnly;

    [Glyph(Glyphs.Sort)]
    [DisplayName("Sort By")]
    public Sort Sort { get; set; } = Sort.NewestFirst;
}
