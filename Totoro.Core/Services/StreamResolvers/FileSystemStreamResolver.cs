using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Services.StreamResolvers;

internal class FileSystemStreamResolver(string directory, ISettings settings) : IVideoStreamModelResolver, IDisposable
{
    private readonly DirectoryInfo _directory = new(directory);
    private readonly string[] _videoFileExtensions = [".mkv", ".mp4"];
    private readonly Dictionary<int, string> _episodes = [];
    private readonly ISettings _settings = settings;
    private static Stream _prevStream;

    public Task<EpisodeModelCollection> ResolveAllEpisodes(StreamType streamType)
    {
        foreach (var fileInfo in _directory.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            if (!_videoFileExtensions.Contains(fileInfo.Extension.ToLower()))
            {
                continue;
            }

            var parseResult = AnitomySharp.AnitomySharp.Parse(fileInfo.Name);
            var ep = parseResult.FirstOrDefault(x => x.Category == AnitomySharp.Element.ElementCategory.ElementEpisodeNumber)?.Value;

            if (!int.TryParse(ep, out int episode))
            {
                continue;
            }

            _episodes.Add(episode, fileInfo.FullName);
        }

        return Task.FromResult(EpisodeModelCollection.FromEpisodes(_episodes.Keys.Order(), _settings.SkipFillers));
    }

    public Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, StreamType streamType)
    {
        _prevStream?.Dispose();
        _prevStream = null;

        if (!_episodes.TryGetValue(episode, out string value))
        {
            return Task.FromResult<VideoStreamsForEpisodeModel>(default);
        }

        _prevStream = File.OpenRead(value);
        return Task.FromResult(new VideoStreamsForEpisodeModel(_prevStream));
    }

    public void Dispose()
    {
        _prevStream?.Dispose();
        _prevStream = null;
    }
}
