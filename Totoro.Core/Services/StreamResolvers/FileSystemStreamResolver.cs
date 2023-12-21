using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Services.StreamResolvers;

internal class FileSystemStreamResolver : IVideoStreamModelResolver, IDisposable
{
    private readonly DirectoryInfo _directory;
    private readonly string[] _videoFileExtensions = new[] { ".mkv", ".mp4" };
    private readonly Dictionary<int, string> _episodes = [];
    private static Stream _prevStream;

    public FileSystemStreamResolver(string directory)
    {
        _directory = new(directory);
    }

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

        return Task.FromResult(EpisodeModelCollection.FromEpisodes(_episodes.Keys.Order()));
    }

    public Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, StreamType streamType)
    {
        _prevStream?.Dispose();
        _prevStream = null;

        if (!_episodes.ContainsKey(episode))
        {
            return Task.FromResult<VideoStreamsForEpisodeModel>(default);
        }

        _prevStream = File.OpenRead(_episodes[episode]);
        return Task.FromResult(new VideoStreamsForEpisodeModel(_prevStream));
    }

    public void Dispose()
    {
        _prevStream?.Dispose();
        _prevStream = null;
    }
}
