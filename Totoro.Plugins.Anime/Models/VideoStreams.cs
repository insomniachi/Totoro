using System.Collections.ObjectModel;

namespace Totoro.Plugins.Anime.Models;

public class VideoStreams : Collection<VideoStream>
{
    public VideoStream? GetStream(string resolution) => this.FirstOrDefault(x => x.Resolution == resolution);
}
