using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.YugenAnime;

public class Config : AnimeProviderConfigObject
{
    [Description("Url to home page")]
    [Glyph(Glyphs.Url)]
    public string Url { get; set; } = "https://yugenanime.tv/";

    [DisplayName("Stream Type")]
    [Description("Choose what to play by default, sub/dub")]
    [AllowedValues(@"English Subbed", @"English Dubbed")]
    [Glyph(Glyphs.StreamType)]
    public StreamType StreamType { get; set; } = StreamType.Subbed(Languages.English);
}
