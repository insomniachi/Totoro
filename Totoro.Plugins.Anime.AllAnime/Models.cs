using System.Diagnostics;

namespace Totoro.Plugins.Anime.AllAnime;

#nullable disable

class GetVersionResponse
{
    public string data { get; set; } = string.Empty;
    public string episodeIframeHead { get; set; } = string.Empty;
}

class EpisodeDetails
{
    public List<string> sub { get; set; } = new();
    public List<string> dub { get; set; } = new();
    public List<string> raw { get; set; } = new();
}

class StreamLink
{
    public string link { get; set; } = string.Empty;
    public bool hls { get; set; }
    public string resolutionStr { get; set; } = string.Empty;
    public int resolution { get; set; } = 0;
    public string src { get; set; } = string.Empty;
    public PortData portData { get; set; }
}

[DebuggerDisplay("{priority} - {sourceUrl} - {type}")]
class SourceUrlObj
{
    public string sourceUrl { get; set; }
    public double priority { get; set; }
    public string type { get; set; }
}

class PortDataStream
{
    public string format { get; set; }
    public string url { get; set; }
    public string audio_lang { get; set; }
    public string hardsub_lang { get; set; }
}

class PortData
{
    public List<PortDataStream> streams { get; set; }
}
