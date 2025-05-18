using System.ComponentModel;
using Flurl;
using Totoro.Plugins.Contracts;
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

public class ConfigNew(IPluginConfiguration pluginConfig) : PluginConfiguration<ConfigNew>(pluginConfig)
{
	[Description("Url to home page")]
	[Glyph(Glyphs.Url)]
	public Uri Url { get; set => SetValue(ref field, value); } = new Uri("https://animepahe.ru/");

	protected override ConfigNew CreateDefault() => new(_pluginConfig);

	protected override void Load(ConfigNew options)
    {
        Url = options.Url;
	}
}
