using System.Text.Json.Serialization;

namespace Totoro.Core.Models;

public record AnimeId
{
    [JsonPropertyName("anidb")]
    public long? AniDb { get; set; }

    [JsonPropertyName("anilist")]
    public long AniList { get; set; }

    [JsonPropertyName("myanimelist")]
    public long MyAnimeList { get; set; }

    [JsonPropertyName("kitsu")]
    public long? Kitsu { get; set; }

    public static AnimeId Zero => new();
}

[JsonSerializable(typeof(AnimeId))]
public partial class AnimeIdSerializerContext : JsonSerializerContext
{

}
