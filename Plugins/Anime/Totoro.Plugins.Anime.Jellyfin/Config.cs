using System.ComponentModel;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Jellyfin;

public class Config : AnimeProviderConfigObject
{
	[Description("Url to jellyfin instance")]
	[Glyph(Glyphs.Url)]
	public string BaseUrl { get; set; } = @"";

	[Description("Id of user with access to anime library")]
	[Glyph(Glyphs.People)]
	public string UserId { get; set; } = @"";

	[Description("Id of Anime library")]
	[Glyph(Glyphs.Library)]
	public string LibraryId { get; set; } = @"";

	[Description("Jellyfin api key")]
	[Glyph(Glyphs.Unlock)]
	public string ApiKey { get; set; } = @"";

}
