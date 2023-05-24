using Totoro.Plugins.Contracts.Optional;

namespace Totoro.Core.Services.MediaEvents;

internal class DiscordRichPresenseUpdater : MediaEventListener
{
    private readonly IDiscordRichPresense _discordRichPresense;
    private readonly ISettings _settings;
    private TimeSpan _currentTime;
    private TimeSpan _duration;

    public DiscordRichPresenseUpdater(IDiscordRichPresense discordRichPresense,
                                      ISettings settings)
    {
        _discordRichPresense = discordRichPresense;
        _settings = settings;
    }

    protected override void OnDurationChanged(TimeSpan duration)
    {
        _duration = duration;

        if (!_settings.UseDiscordRichPresense)
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

        _discordRichPresense.UpdateDetails(GetTitle());
        _discordRichPresense.UpdateState($"Episode {_currentEpisode}");
        _discordRichPresense.UpdateTimer(_duration - _currentTime);
        _discordRichPresense.UpdateImage(GetDiscordImageKey());
    }

    protected override void OnPaused()
    {
        if (!_settings.UseDiscordRichPresense)
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

        if(_animeModel is not null)
        {
            return _animeModel.Image;
        }

        return "icon";
    }

    private string GetTitle() => _animeModel?.Title ?? _searchResult?.Title ?? string.Empty;
}

