using AnimDL.Api;
using Totoro.Core.Tests.Helpers;
using Totoro.Core.ViewModels;

namespace Totoro.Core.Tests.Builders;

internal class WatchViewModelBuilder
{
    private readonly Mock<IDiscordRichPresense> _discordRpcMock = new();
    private readonly Mock<IPlaybackStateStorage> _playbackStateStorageMock = new();
    private readonly Mock<ITrackingService> _trackingServiceMock = new();
    private readonly Mock<IProviderFactory> _providerFactoryMock = new();
    private readonly Mock<IViewService> _viewServiceMock = new();
    private readonly Mock<ISettings> _settingsMock = new();
    private readonly Mock<IAnimeService> _animeServiceMock = new();
    private readonly Mock<ITimestampsService> _timestampsServiceMock = new();
    private readonly Mock<IRecentEpisodesProvider> _recentEpisodesProviderMock = new();

    internal MockMediaPlayer MediaPlayer { get; } = new();

    internal WatchViewModelBuilder()
    {
    }

    internal WatchViewModel Bulid()
    {
        return new WatchViewModel(_providerFactoryMock.Object,
                                  _trackingServiceMock.Object,
                                  _viewServiceMock.Object,
                                  _settingsMock.Object,
                                  _playbackStateStorageMock.Object,
                                  _discordRpcMock.Object,
                                  _animeServiceMock.Object,
                                  MediaPlayer,
                                  _timestampsServiceMock.Object,
                                  _recentEpisodesProviderMock.Object,
                                  Mock.Of<ILocalMediaService>());
    }

    internal WatchViewModelBuilder WithProviderFactory(Action<Mock<IProviderFactory>> configure)
    {
        configure(_providerFactoryMock);
        return this;
    }

    internal WatchViewModelBuilder WithTrackingService(Action<Mock<ITrackingService>> configure)
    {
        configure(_trackingServiceMock);
        return this;
    }

    internal WatchViewModelBuilder WithViewService(Action<Mock<IViewService>> configure)
    {
        configure(_viewServiceMock);
        return this;
    }

    internal WatchViewModelBuilder WithSettings(Action<Mock<ISettings>> configure)
    {
        configure(_settingsMock);
        return this;
    }

    internal WatchViewModelBuilder WithPlaybackStateStorage(Action<Mock<IPlaybackStateStorage>> configure)
    {
        configure(_playbackStateStorageMock);
        return this;
    }

    internal WatchViewModelBuilder WithDiscordRpc(Action<Mock<IDiscordRichPresense>> configure)
    {
        configure(_discordRpcMock);
        return this;
    }

    internal WatchViewModelBuilder WithAnimeService(Action<Mock<IAnimeService>> configure)
    {
        configure(_animeServiceMock);
        return this;
    }

    internal WatchViewModelBuilder WithTimeStampService(Action<Mock<ITimestampsService>> configure)
    {
        configure(_timestampsServiceMock);
        return this;
    }

    internal WatchViewModelBuilder WithRecentEpisodesProvider(Action<Mock<IRecentEpisodesProvider>> configure)
    {
        configure(_recentEpisodesProviderMock);
        return this;
    }

    internal void VerifyDiscordRpc(Action<Mock<IDiscordRichPresense>> verify) => verify(_discordRpcMock);
    internal void VerifyPlaybackStateStorage(Action<Mock<IPlaybackStateStorage>> verify) => verify(_playbackStateStorageMock);
    internal void VerifyTrackingService(Action<Mock<ITrackingService>> verify) => verify(_trackingServiceMock);

}
