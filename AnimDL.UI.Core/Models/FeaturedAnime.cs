using System.Text.Json.Serialization;

namespace AnimDL.UI.Core.Models;

public class FeaturedAnime
{
    public string Id => Url?.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(1).FirstOrDefault();
    public string[] GenresArray => Genres?.Split(",");

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("img")]
    public string Image { get; set; }

    [JsonPropertyName("genre")]
    public string Genres { get; set; }

    [JsonPropertyName("desc")]
    public string Description { get; set; }
}