using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Totoro.Plugins.Anime.Models;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.AllAnime;

public class Config : AnimeProviderConfigObject
{
    [Description("Url to home page")]
    [Glyph(Glyphs.Url)]
    public string Url { get; set; } = "https://allanime.to";

    [DisplayName("Stream Type")]
    [Description("Choose what to play by default, sub/dub")]
    [AllowedValues(@"English Subbed", @"English Dubbed", @"Raw")]
    [Glyph(Glyphs.StreamType)]
    public StreamType StreamType { get; set; } = StreamType.Subbed(Languages.English);

    [DisplayName("Country Of Origin")]
    [Description("Filter anime by country")]
    [AllowedValues("ALL", "JP", "CN", "KR")]
    [Glyph("\uF2B7")]
    public string CountryOfOrigin { get; set; } = "JP";

    [IgnoreDataMember]
    public string Api = "https://api.allanime.day/api";
}

public class ConfigNew(IPluginConfiguration pluginConfig) : PluginConfiguration<ConfigNew>(pluginConfig)
{
	[Description("Url to home page")]
	[Glyph(Glyphs.Url)]
	public required string Url { get; set => SetValue(ref field, value); } = "https://allanime.to";

	[DisplayName("Stream Type")]
	[Description("Choose what to play by default, sub/dub")]
	[AllowedValues(@"English Subbed", @"English Dubbed", @"Raw")]
	[Glyph(Glyphs.StreamType)]
	public StreamType StreamType { get; set => SetValue(ref field, value); } = StreamType.Subbed(Languages.English);

	[DisplayName("Country Of Origin")]
	[Description("Filter anime by country")]
	[AllowedValues("ALL", "JP", "CN", "KR")]
	[Glyph("\uF2B7")]
	public string CountryOfOrigin { get; set => SetValue(ref field, value); } = "JP";

	public const string Api = "https://api.allanime.day/api";

	protected override void Load(ConfigNew options)
	{
		Url = options.Url;
		StreamType = options.StreamType;
		CountryOfOrigin = options.CountryOfOrigin;
	}

	protected override ConfigNew CreateDefault() => new(_pluginConfig) { Url = "https://allanime.to/" };
}
