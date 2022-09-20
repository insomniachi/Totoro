using System.Text.Json.Serialization;

namespace Totoro.Core.Models;

public record AnimeId
{
    [JsonPropertyName("anidb")]
    public long AniDb { get; set; }

    [JsonPropertyName("anilist")]
    public long AniList { get; set; }

    [JsonPropertyName("myanimelist")]
    public long MyAnimeList { get; set; }

    [JsonPropertyName("kitsu")]
    public long Kitsu { get; set; }
}
