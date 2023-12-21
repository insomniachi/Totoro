using Totoro.Core.Services.MediaEvents;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.WinUI.ViewModels;

public class NativeMediaPlayerTrackingUpdater : TrackingUpdater
{
    public NativeMediaPlayerTrackingUpdater(ITrackingServiceContext trackingService,
                                            ISettings settings,
                                            IViewService viewService,
                                            TimeProvider timeProvider) : base(trackingService, settings, viewService, timeProvider)
    {
    }

    public void SetMediaPlayer(IHavePosition mediaPlayer)
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

    public void SetMediaPlayer(IHavePosition mediaPlayer)
    {
        mediaPlayer.DurationChanged.Subscribe(OnDurationChanged);
        mediaPlayer.PositionChanged.Subscribe(OnPositionChanged);
    }

    public void Initialize()
    {
        OnPlay();
    }
}
