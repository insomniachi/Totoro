using System.Text.Json.Serialization;

namespace AnimDL.UI.Core.Models
{
    class MediaResponse
    {
        public Media Media { get; set; }
    }

    class Media
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }
    }


    class ShowResponse
    {
        [JsonPropertyName("findShowsByExternalId")]
        public Show[] Shows { get; set; }
    }


    class Show
    {
        [JsonPropertyName("episodes")]
        public Episode[] Episodes { get; set; }
    }

    class Episode
    {
        [JsonPropertyName("number")]
        public string EpisodeNumber { get; set; }

        [JsonPropertyName("timestamps")]
        public TimeStamp[] TimeStamps { get; set; }
    }

    class TimeStamp
    {
        [JsonPropertyName("type")]
        public TimeStampType Type { get; set; }

        [JsonPropertyName("at")]
        public double Time { get; set; }
    }

    class TimeStampType
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }
    }
}
