using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Models
{
    public class VideoStreamModel
    {
        private VideoStreamModel() { }

        public string StreamUrl { get; init; }
        public Dictionary<string, string> Headers { get; init; }
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
                StreamUrl = link.StreamLink
            };
        }
    }
}
