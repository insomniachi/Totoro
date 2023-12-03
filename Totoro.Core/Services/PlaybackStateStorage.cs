using Splat;

namespace Totoro.Core.Services;

public class PlaybackStateStorage : IResumePlaybackService, IEnableLogger
{
    private readonly Dictionary<long, Dictionary<int, double>> _recents;
    private readonly ILocalSettingsService _localSettingsService;

    public PlaybackStateStorage(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        _recents = _localSettingsService.ReadSetting<Dictionary<long, Dictionary<int, double>>>("Recents", new());
    }

    public double GetTime(long id, int episode)
    {
        this.Log().Info($"Checking saved time for Anime: {id}, Episode: {episode}");

        if (!_recents.ContainsKey(id))
        {
            this.Log().Info("Saved time not found");
            return 0;
        }

        if (!_recents[id].ContainsKey(episode))
        {
            this.Log().Info("Saved time not found");
            return 0;
        }

        var time = _recents[id][episode];

        this.Log().Info($"Saved time found {time}");
        return time;
    }

    public void Reset(long id, int episode)
    {
        if (!_recents.ContainsKey(id))
        {
            return;
        }

        if (!_recents[id].ContainsKey(episode))
        {
            return;
        }

        this.Log().Info("Reset time for Id:{0} Ep:{1}", id, episode);

        _recents[id].Remove(episode);

        if (_recents[id].Count == 0)
        {
            _recents.Remove(id);
        }
    }

    public void SaveState()
    {
        _localSettingsService.SaveSetting("Recents", _recents);
        this.Log().Info("Saved playback state");
    }

    public void Update(long id, int episode, double time)
    {
        if (!_recents.ContainsKey(id))
        {
            _recents.Add(id, new Dictionary<int, double>() { [episode] = time });
            return;
        }

        if (!_recents[id].ContainsKey(episode))
        {
            _recents[id].Add(episode, time);
            return;
        }

        _recents[id][episode] = time;
    }
}
