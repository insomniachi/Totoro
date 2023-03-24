using Totoro.Core.Services.Debrid;

namespace Totoro.Core.Models
{
    public class VideoStreamsForEpisodeModel
    {
        private readonly Dictionary<string, VideoStreamModel> _streams = new();
        private readonly IEnumerable<string> _streamTypes = Enumerable.Empty<string>();
        private readonly IEnumerable<string> _qualities = Enumerable.Empty<string>();

        public VideoStreamsForEpisodeModel(VideoStreamsForEpisode streamsForEp)
        {
            _qualities = streamsForEp.Qualities.Select(x => x.Key);
            _streamTypes = streamsForEp.StreamTypes;
            foreach (var item in streamsForEp.Qualities)
            {
                var model = VideoStreamModel.FromVideoStream(item.Value);
                foreach (var kv in streamsForEp.AdditionalInformation)
                {
                    model.AdditionalInformation.Add(kv.Key, kv.Value);
                }
                _streams.Add(item.Key, model);
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
            var model = VideoStreamModel.FromStream(stream);
            model.AdditionalInformation.Add("IsMKV", "true");
            _streams.Add("default", model);
        }

        public VideoStreamModel GetStreamModel(string quality) => _streams[quality];
        public IEnumerable<string> StreamTypes => _streamTypes;
        public IEnumerable<string> Qualities => _qualities;
    }
}
