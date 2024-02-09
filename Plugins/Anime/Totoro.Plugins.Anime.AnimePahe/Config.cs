using System.ComponentModel;
using Totoro.Plugins.Helpers;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AnimePahe;

public class Config : AnimeProviderConfigObject
{
    [Description("Url to home page")]
    [Glyph(Glyphs.Url)]
    public string Url { get; set; } = "https://animepahe.ru/";

    public Dictionary<string,string> GetHeaders()
    {
        return new Dictionary<string, string>
        {
            { HeaderNames.Referer, Url },
            { HeaderNames.Cookie, "__ddg2_=YW5pbWRsX3NheXNfaGkNCg.;" }
        };
    }
}
