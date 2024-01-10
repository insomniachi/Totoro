using System.ComponentModel;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Torrents.Nya;

public class Config : ConfigObject
{
    [Glyph(Glyphs.Url)]
    public string Url { get; set; } = "https://nyaa.ink/";

    [Glyph(Glyphs.Filter)]
    public Filter Filter { get; set; } = Filter.TrustedOnly;

    [Glyph(Glyphs.Category)]
    public Category Category { get; set; } = Category.Anime;

    [Glyph(Glyphs.Sort)]
    [DisplayName("Sort By")]
    public SortBy SortBy { get; set; } = SortBy.Date;
    
    [Glyph(Glyphs.SortDirection)]
    [DisplayName("Sort Direction")]
    public SortDirection SortDirection { get; set; } = SortDirection.Descending;
}
