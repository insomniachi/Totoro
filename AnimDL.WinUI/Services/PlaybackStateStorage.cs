using System.Collections.Generic;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Core.Contracts;

namespace AnimDL.WinUI.Core.Services;

public class PlaybackStateStorage : IPlaybackStateStorage
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
        if(!_recents.ContainsKey(id))
        {
            return 0;
        }

        if (!_recents[id].ContainsKey(episode))
        {
            return 0;
        }

        return _recents[id][episode];
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

        _recents[id].Remove(episode);

        if (_recents[id].Count == 0)
        {
            _recents.Remove(id);
        }
    }

    public void StoreState()
    {
        _localSettingsService.SaveSetting("Recents", _recents);
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
