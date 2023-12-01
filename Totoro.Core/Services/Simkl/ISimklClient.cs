using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Refit;

namespace Totoro.Core.Services.Simkl;

internal interface ISimklClient
{
    [Get("/sync/all-items/{type}/{status}")]
    Task<SimklAllItemsResponse> GetAllItems(ItemType type, SimklWatchStatus status);

    [Get("/anime/{id}?client_id={clientId}&extended=full")]
    Task<SimklAnimeMetaData> GetSummary(long id, string clientId = "0a814ce1ee4819adcbcee198151e256f0700cc8c3976ad3084c8a329720124fc");
}

internal class SimklHandler : DelegatingHandler
{
    private readonly string _token = "002e7f8f88f5be46c0a2a6dd60b669b9486f26e9324fd566f4fa31fa8c770501";


    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("simkl-api-key", "0a814ce1ee4819adcbcee198151e256f0700cc8c3976ad3084c8a329720124fc");
        request.Headers.Add("Authorization", $"Bearer {_token}");
        return base.SendAsync(request, cancellationToken);
    }
}


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

internal class SimklAllItemsResponse
{
    [JsonPropertyName("anime")]
    public List<SimklAnimeModel> Anime { get; set; }
}


internal class SimklAnimeModel
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
    public SimklAnimeMetaData Show { get; set; }
}

internal class SimklAnimeMetaData
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("ids")]
    public SimklIds Id { get; set; }

    [JsonPropertyName("en_title")]
    public string EnglishTitle { get; set; }

    [JsonPropertyName("season_name_year")]
    public string Season { get; set; }

    [JsonPropertyName("poster")]
    public string Image { get; set; }

    [JsonPropertyName("overview")]
    public string Overview { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get;set; }

    [JsonPropertyName("total_episodes")]
    public int TotalEpisodes { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("ratings")]
    public SimklRating Ratings { get; set; }

    [JsonPropertyName("trailers")]
    public List<Trailer> Trailers { get; set; }

    [JsonPropertyName("users_recommendations")]
    public List<SimklAnimeMetaData> UserRecommendations { get; set; }

    [JsonPropertyName("relations")]
    public List<SimklAnimeMetaData> Relations { get; set; }
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
    public double Rating { get; set; }

    [JsonPropertyName("votes")]
    public long Votes { get; set; }

    [JsonPropertyName("rank")]
    public int? Rank { get; set; }
}


internal class SimklIds
{
    [JsonPropertyName("simkl")]
    public long Simkl { get; set; }

    [JsonPropertyName("mal")]
    public string MyAnimeList { get; set; }

    [JsonPropertyName("anilist")]
    public string Anilist { get; set; }

    [JsonPropertyName("kitsu")]
    public string Kitsu { get; set; }

    [JsonPropertyName("anidb")]
    public string AniDb { get; set; }
}