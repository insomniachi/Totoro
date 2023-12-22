using Splat;

namespace Totoro.Core.Services;

public class PlaybackStateStorage : IResumePlaybackService, IEnableLogger
{
    private readonly Dictionary<long, Dictionary<int, double>> _recents;
    private readonly IKnownFolders _knownFolders;
    private readonly IFileService _fileService;
    private readonly string _fileName = @"recents.json";

    public PlaybackStateStorage(IKnownFolders knownFolders,
                                IFileService fileService)
    {
        _recents = fileService.Read<Dictionary<long, Dictionary<int, double>>>(knownFolders.ApplicationData, _fileName) ?? [];
        _knownFolders = knownFolders;
        _fileService = fileService;
    }

    public double GetTime(long id, int episode)
    {
        this.Log().Info($"Checking saved time for Anime: {id}, Episode: {episode}");

        if (!_recents.TryGetValue(id, out Dictionary<int, double> anime))
        {
            this.Log().Info("Saved time not found");
            return 0;
        }

        if (!anime.TryGetValue(episode, out double time))
        {
            this.Log().Info("Saved time not found");
            return 0;
        }

        this.Log().Info($"Saved time found {time}");
        return time;
    }

    public void Reset(long id, int episode)
    {
        if (!_recents.TryGetValue(id, out Dictionary<int, double> anime))
        {
            return;
        }

        if (!anime.ContainsKey(episode))
        {
            return;
        }

        this.Log().Info("Reset time for Id:{0} Ep:{1}", id, episode);
        anime.Remove(episode);

        if (anime.Count == 0)
        {
            _recents.Remove(id);
        }
    }

    public void SaveState()
    {
        _fileService.Save(_knownFolders.ApplicationData, _fileName, _recents);
        this.Log().Info("Saved playback state");
    }

    public void Update(long id, int episode, double time)
    {
        if (!_recents.TryGetValue(id, out Dictionary<int, double> value))
        {
            value = new Dictionary<int, double>() { [episode] = time };
            _recents.Add(id, value);
            return;
        }

        if (!value.ContainsKey(episode))
        {
            value.Add(episode, time);
            return;
        }

        value[episode] = time;
    }
}
