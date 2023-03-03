namespace Totoro.Core.Torrents;

public interface ISubtitlesDownloader
{
    Task<IEnumerable<KeyValuePair<string, string>>> DownloadSubtitles(string url);
}
