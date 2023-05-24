using MonoTorrent.Client;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Contracts;


public interface IVideoStreamModelResolver
{
    Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, StreamType streamType);
    Task<EpisodeModelCollection> ResolveAllEpisodes(StreamType streamType);
}

public interface ICompletionAware
{
    void OnCompleted();
}

public interface INotifyDownloadStatus
{
    IObservable<(double, ConnectionMonitor)> Status { get; }
}

