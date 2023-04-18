using MonoTorrent.Client;

namespace Totoro.Core.Contracts;


public interface IVideoStreamModelResolver
{
    Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, string subStream);
    Task<EpisodeModelCollection> ResolveAllEpisodes(string subStream);
}

public interface ICompletionAware
{
    void OnCompleted();
}

public interface INotifyDownloadStatus
{
    IObservable<(double, ConnectionMonitor)> Status { get; }
}

