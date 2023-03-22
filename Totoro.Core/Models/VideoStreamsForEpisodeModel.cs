using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Models
{
    public class VideoStreamsForEpisodeModel
    {
        private readonly Dictionary<string, VideoStreamModel> _streams = new();
        private readonly IEnumerable<string> _streamTypes = Enumerable.Empty<string>();
        private readonly IEnumerable<string> _qualities = Enumerable.Empty<string>();
        private readonly Dictionary<string, string> _additionalInformation = new();

        public VideoStreamsForEpisodeModel(VideoStreamsForEpisode streamsForEp)
        {
            _qualities = streamsForEp.Qualities.Select(x => x.Key);
            _streamTypes = streamsForEp.StreamTypes;
            _additionalInformation = streamsForEp.AdditionalInformation;
            foreach (var item in streamsForEp.Qualities)
            {
                _streams.Add(item.Key, VideoStreamModel.FromVideoStream(item.Value));
            }
        }

        public VideoStreamsForEpisodeModel(DirectDownloadLink link)
        {
            _qualities = new[] { "default" };
            _streams.Add("default", VideoStreamModel.FromDirectDownloadLink(link));
        }

        public VideoStreamsForEpisodeModel(string url)
        {
            _qualities = new[] { "default" };
            _streams.Add("default", VideoStreamModel.FromUrl(url));
        }

        public VideoStreamsForEpisodeModel(Stream stream)
        {
            _qualities = new[] { "default" };
            _streams.Add("default", VideoStreamModel.FromStream(stream));
        }

        public VideoStreamModel GetStreamModel(string quality) => _streams[quality];
        public IEnumerable<string> StreamTypes => _streamTypes;
        public Dictionary<string, string> AdditionalInformation => _additionalInformation;
        public IEnumerable<string> Qualities => _qualities;
    }
}
