using System.Text.Json.Serialization;

namespace Totoro.Modules.AnimeProviders.Senshi;

public class SenshiItem
{
	[JsonPropertyName("id")] public int InternalId { get; set; }
	[JsonPropertyName("public_id")] public string Id { get; set; } = string.Empty;
	[JsonPropertyName("anime_picture")] public string? Image { get; set; }
	[JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
	[JsonPropertyName("anilist_id")] public long? AnilistId { get; set; }
}

public class SenshiSearchResponse
{
	[JsonPropertyName("data")] public List<SenshiItem> Items { get; set; } = [];
}

public class SenshiEpisode
{
	[JsonPropertyName("ep_id")] public float Episode { get; set; }
	[JsonPropertyName("ep_title")] public string Title { get; set; } = string.Empty;
}

public class SenshiStream
{
	[JsonPropertyName("url")] public string StreamUrl { get; set; } = string.Empty;
	[JsonPropertyName("status")] public string Type { get; set; } = string.Empty;
}
