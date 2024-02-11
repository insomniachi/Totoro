using System.ComponentModel;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Aniwave;

public class Config : AnimeProviderConfigObject
{
    [Description("Url to home page")]
    [Glyph(Glyphs.Url)]
    public string Url { get; set; } = "https://aniwave.to";
}
