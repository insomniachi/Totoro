using System.ComponentModel;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Anime4Up;

public class Config : AnimeProviderConfigObject
{
    [Description("Url to home page")]
    [Glyph(Glyphs.Url)]
    public string Url { get; set; } = "https://anime4up.top/";
}
