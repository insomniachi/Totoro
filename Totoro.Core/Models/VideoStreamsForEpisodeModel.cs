using Totoro.Core.Services.Debrid;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Models
{
    public class VideoStreamsForEpisodeModel
    {
        private readonly Dictionary<string, VideoStreamModel> _streams = [];
        private readonly IEnumerable<StreamType> _streamTypes = Enumerable.Empty<StreamType>();
        private readonly IEnumerable<string> _qualities = Enumerable.Empty<string>();

        public VideoStreamsForEpisodeModel(VideoStreamsForEpisode streamsForEp)
        {
            _qualities = streamsForEp.Streams.Select(x => x.Resolution);
            _streamTypes = streamsForEp.StreamTypes;
            foreach (var item in _qualities)
            {
                var model = VideoStreamModel.FromVideoStream(streamsForEp.Streams.GetStream(item));
                model.AdditionalInformation = streamsForEp.AdditionalInformation;
                _streams.Add(item, model);
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
            model.AdditionalInformation.IsMkv = true;
            _streams.Add("default", model);
        }

        public VideoStreamModel GetStreamModel(string quality) => _streams[quality];
        public IEnumerable<StreamType> StreamTypes => _streamTypes;
        public IEnumerable<string> Qualities => _qualities;
    }
}
