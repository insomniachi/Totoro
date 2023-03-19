namespace Totoro.Core.Services;


public interface IVideoStreamModelResolver
{
    Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, string subStream);
    Task<EpisodeModelCollection> ResolveAllEpisodes(string subStream);
}

