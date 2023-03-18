namespace Totoro.Core.Services;


public interface IVideoStreamModelResolver
{
    Task<VideoStreamsForEpisodeModel> Resolve(int episode, string subStream);
    Task<int> GetNumberOfEpisodes(string subStream);
}

