using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Totoro.Core.Services.Simkl;

internal enum ItemType
{
    [EnumMember(Value = "shows")]
    Shows,

    [EnumMember(Value = "movies")]
    Movies,

    [EnumMember(Value = "anime")]
    Anime
}

internal enum SimklWatchStatus
{
    [EnumMember(Value = "watching")]
    Watching,

    [EnumMember(Value = "plantowatch")]
    PlanToWatch,

    [EnumMember(Value = "hold")]
    Hold,

    [EnumMember(Value = "completed")]
    Completed,

    [EnumMember(Value = "dropped")]
    Dropped
}

internal class SimklAllItems
{
    [JsonPropertyName("anime")]
    public List<SimklItemModel> Anime { get; set; }

    [JsonPropertyName("movies")]
    public List<SimklItemModel> Movies { get; set; }

    [JsonPropertyName("shows")]
    public List<SimklItemModel> Shows { get; set; }
}

internal class SimklMutateListBody
{
    [JsonPropertyName("shows")]
    public List<SimklMetaDataSlim> Shows { get; set; }

    [JsonPropertyName("movies")]
    public List<SimklMetaDataSlim> Movies { get; set; }

    [JsonPropertyName("episodes")]
    public List<EpisodeSlim> Episodes { get; set; }
}

internal class SimklMetaDataSlim
{
    [JsonPropertyName("ids")]
    public SimklIds Ids { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("rating")]
    public int? Rating { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("to")]
    public string To { get; set; }

}

internal class Episode
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("Description")]
    public string Description { get; set; }

    [JsonPropertyName("episode")]
    public int EpisodeNumber { get; set; }

    [JsonPropertyName("aired")]
    public bool Aired { get; set; }

    [JsonPropertyName("img")]
    public string Image { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("ids")]
    public SimklIdSlim Ids { get; set; }
}

internal class SimklIdSlim
{
    [JsonPropertyName("simkl_id")]
    public long Simkl { get; set; }
}

internal class EpisodeSlim
{
    [JsonPropertyName("watched_at")]
    public string WatchedAt { get; set; }

    [JsonPropertyName("ids")]
    public SimklIds Ids { get; set; }
}


internal class SimklItemModel
{
    [JsonPropertyName("last_watched_at")]
    public string LastWatchedAt { get; set; }

    [JsonPropertyName("user_rating")]
    public int? UserRating { get; set; }

    [JsonPropertyName("last_watched")]
    public string LastWatchedEpisode { get; set; }

    [JsonPropertyName("next_to_watch")]
    public string NextEpisode { get; set; }

    [JsonPropertyName("watched_episodes_count")]
    public int WatchedEpisodeCount { get; set; }

    [JsonPropertyName("total_episodes_count")]
    public int TotalEpisodesCount { get; set; }

    [JsonPropertyName("not_aired_episodes_count")]
    public int NotAiredEpisodesCount { get; set; }

    [JsonPropertyName("anime_type")]
    public string AnimeType { get; set; }

    [JsonPropertyName("show")]
    public SimklMetaData Show { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }
}

internal class SimklMetaData
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("ids")]
    public SimklIds Id { get; set; }

    [JsonPropertyName("title_en")]
    public string EnglishTitle { get; set; }

    [JsonPropertyName("season_name_year")]
    public string Season { get; set; }

    [JsonPropertyName("poster")]
    public string Image { get; set; }

    [JsonPropertyName("overview")]
    public string Overview { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; }

    [JsonPropertyName("total_episodes")]
    public int TotalEpisodes { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("ratings")]
    public SimklRating Ratings { get; set; }

    [JsonPropertyName("trailers")]
    public List<Trailer> Trailers { get; set; }

    [JsonPropertyName("users_recommendations")]
    public List<SimklMetaData> UserRecommendations { get; set; }

    [JsonPropertyName("relations")]
    public List<SimklMetaData> Relations { get; set; }
}

internal class SimklRating
{
    [JsonPropertyName("simkl")]
    public RatingItem Simkl { get; set; }

    [JsonPropertyName("mal")]
    public RatingItem Mal { get; set; }
}

internal class Trailer
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("youtube")]
    public string Youtube { get; set; }
}


internal class RatingItem
{
    [JsonPropertyName("rating")]
    public float Rating { get; set; }

    [JsonPropertyName("votes")]
    public long Votes { get; set; }

    [JsonPropertyName("rank")]
    public int? Rank { get; set; }
}


internal class SimklIds
{
    [JsonPropertyName("simkl")]
    public long? Simkl { get; set; }

    [JsonPropertyName("simkl_id")]
    public long? Simkl2 { get; set; }

    [JsonPropertyName("mal")]
    public string MyAnimeList { get; set; }

    [JsonPropertyName("anilist")]
    public string Anilist { get; set; }

    [JsonPropertyName("kitsu")]
    public string Kitsu { get; set; }

    [JsonPropertyName("anidb")]
    public string AniDb { get; set; }
}