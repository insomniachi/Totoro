using Totoro.Plugins.Contracts.Optional;

namespace Totoro.Core.Services.MediaEvents;

public class DiscordRichPresenseUpdater : MediaEventListener, IDisposable
{
    private readonly IDiscordRichPresense _discordRichPresense;
    private readonly ISettings _settings;
    private TimeSpan _currentTime;
    private TimeSpan _duration;
    private bool _disposed;

    public DiscordRichPresenseUpdater(IDiscordRichPresense discordRichPresense,
                                      ISettings settings)
    {
        _discordRichPresense = discordRichPresense;
        _settings = settings;
    }

    protected override void OnDurationChanged(TimeSpan duration)
    {
        _duration = duration;

        if (!_settings.UseDiscordRichPresense || !_settings.ShowTimeRemainingOnDiscordRichPresense)
        {
            return;
        }

        _discordRichPresense.UpdateTimer(_duration - _currentTime);
    }

    protected override void OnPositionChanged(TimeSpan position)
    {
        _currentTime = position;
    }

    protected override void OnPlay()
    {
        if (!_settings.UseDiscordRichPresense)
        {
            return;
        }

        _discordRichPresense.SetPresence();
        _discordRichPresense.SetUrl(GetUrl(_animeModel?.Id));
        _discordRichPresense.UpdateDetails(GetTitle());
        _discordRichPresense.UpdateState($"Episode {_currentEpisode}");
        if (_settings.ShowTimeRemainingOnDiscordRichPresense)
        {
            _discordRichPresense.UpdateTimer(_duration - _currentTime);
        }
        _discordRichPresense.UpdateImage(GetDiscordImageKey());
    }

    protected override void OnPaused()
    {
        if (!_settings.UseDiscordRichPresense || _disposed)
        {
            return;
        }

        _discordRichPresense.UpdateState($"Episode {_currentEpisode} (Paused)");
        _discordRichPresense.ClearTimer();
    }

    protected override void OnPlaybackEnded()
    {
        if (!_settings.UseDiscordRichPresense)
        {
            return;
        }

        _discordRichPresense.Clear();
    }

    public override void Stop()
    {
        if (!_settings.UseDiscordRichPresense)
        {
            return;
        }

        _discordRichPresense.Clear();
    }

    private string GetDiscordImageKey()
    {
        if (_searchResult is IHaveImage ihi)
        {
            return ihi.Image;
        }

        if (_animeModel is not null)
        {
            return _animeModel.Image;
        }

        return "icon";
    }

    private string GetTitle() => _animeModel?.Title ?? _searchResult?.Title ?? string.Empty;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual void Dispose(bool disposing)
    {
        if (_disposed || !_settings.UseDiscordRichPresense)
        {
            return;
        }

        if (disposing)
        {
            _discordRichPresense.Clear();
        }

        _disposed = true;
    }

    private string GetUrl(long? id)
    {
        if (id is null)
        {
            return string.Empty;
        }

        return _settings.DefaultListService switch
        {
            ListServiceType.MyAnimeList => $@"https://myanimelist.net/anime/{id}/",
            ListServiceType.AniList => $@"https://anilist.co/anime/{id}/",
            _ => string.Empty
        };
    }
}

