using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Models
{
    public class VideoStreamModel
    {
        private VideoStreamModel() { }
        public Stream Stream { get; set; }
        public string StreamUrl { get; init; }
        public Dictionary<string, string> Headers { get; init; }
        public Dictionary<string, string> AdditionalInformation { get; init; } = new();
        public bool HasHeaders => Headers?.Any() == true;
        public string Quality { get; init; }

        public static VideoStreamModel FromVideoStream(VideoStream stream)
        {
            return new VideoStreamModel
            {
                StreamUrl = stream.Url,
                Headers = stream.Headers,
                Quality = stream.Quality,
            };
        }

        public static VideoStreamModel FromDirectDownloadLink(DirectDownloadLink link)
        {
            return new VideoStreamModel
            {
                StreamUrl = link.Link,
                AdditionalInformation = new Dictionary<string, string> { ["IsMKV"] = "" }
            };
        }

        public static VideoStreamModel FromUrl(string url)
        {
            return new VideoStreamModel
            {
                StreamUrl = url,
            };
        }

        public static VideoStreamModel FromStream(Stream stream)
        {
            return new VideoStreamModel
            {
                Stream = stream
            };
        }
    }
}
