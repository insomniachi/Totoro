using Totoro.Core.Services.MediaEvents;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.WinUI.ViewModels;

public class NativeMediaPlayerTrackingUpdater : TrackingUpdater
{
    public NativeMediaPlayerTrackingUpdater(ITrackingServiceContext trackingService, ISettings settings, IViewService viewService, ISystemClock systemClock) : base(trackingService, settings, viewService, systemClock)
    {
    }

    public void SetMediaPlayer(INativeMediaPlayer mediaPlayer)
    {
        mediaPlayer.DurationChanged.Subscribe(OnDurationChanged);
        mediaPlayer.PositionChanged.Subscribe(OnPositionChanged);
    }
}

public class NativeMediaPlayerDiscordRichPresenseUpdater : DiscordRichPresenseUpdater
{
    public NativeMediaPlayerDiscordRichPresenseUpdater(IDiscordRichPresense discordRichPresense, ISettings settings) : base(discordRichPresense, settings)
    {
    }

    public void SetMediaPlayer(INativeMediaPlayer mediaPlayer)
    {
        mediaPlayer.DurationChanged.Subscribe(OnDurationChanged);
        mediaPlayer.PositionChanged.Subscribe(OnPositionChanged);
    }

    public void Initialize()
    {
        OnPlay();
    }
}
