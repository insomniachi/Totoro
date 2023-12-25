using Splat;

namespace Totoro.Core.Services;

public class PlaybackStateStorage(IKnownFolders knownFolders,
                                  IFileService fileService) : IResumePlaybackService, IEnableLogger
{
    private readonly Dictionary<long, Dictionary<int, double>> _recents = fileService.Read<Dictionary<long, Dictionary<int, double>>>(knownFolders.ApplicationData, FileName) ?? [];
    private readonly IKnownFolders _knownFolders = knownFolders;
    private readonly IFileService _fileService = fileService;
    public const string FileName = @"recents.json";

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
        _fileService.Save(_knownFolders.ApplicationData, FileName, _recents);
        this.Log().Info("Saved playback state");
    }

    public void Update(long id, int episode, double time)
    {
        if(time == 0)
        {
            return;
        }

        if (!_recents.TryGetValue(id, out Dictionary<int, double> value))
        {
            _recents.Add(id, new Dictionary<int, double>() { [episode] = time });
            return;
        }

        value[episode] = time;
    }
}
