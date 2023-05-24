using Totoro.Core.Tests.Helpers;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.Core.Tests.Builders;

internal class WatchViewModelBuilder
{
    private readonly Mock<IDiscordRichPresense> _discordRpcMock = new();
    private readonly Mock<IResumePlaybackService> _playbackStateStorageMock = new();
    private readonly Mock<ITrackingServiceContext> _trackingServiceMock = new();
    private readonly Mock<IPluginFactory<AnimePlugin>> _providerFactoryMock = new();
    private readonly Mock<IViewService> _viewServiceMock = new();
    private readonly Mock<ISettings> _settingsMock = new();
    private readonly Mock<IAnimeServiceContext> _animeServiceMock = new();
    private readonly Mock<ITimestampsService> _timestampsServiceMock = new();

    internal MockMediaPlayer MediaPlayer { get; } = new();

    internal WatchViewModelBuilder()
    {
    }

    //internal WatchViewModel Bulid()
    //{
    //    return new WatchViewModel(_providerFactoryMock.Object,
    //                              _trackingServiceMock.Object,
    //                              _viewServiceMock.Object,
    //                              _settingsMock.Object,
    //                              _playbackStateStorageMock.Object,
    //                              _discordRpcMock.Object,
    //                              _animeServiceMock.Object,
    //                              MediaPlayer,
    //                              _timestampsServiceMock.Object,
    //                              Mock.Of<ILocalMediaService>(),
    //                              Mock.Of<IStreamPageMapper>(),
    //                              Mock.Of<IDebridServiceContext>(),
    //                              Mock.Of<ITorrentCatalog>());
    //}

    internal WatchViewModelBuilder WithProviderFactory(Action<Mock<IPluginFactory<AnimePlugin>>> configure)
    {
        configure(_providerFactoryMock);
        return this;
    }

    internal WatchViewModelBuilder WithTrackingService(Action<Mock<ITrackingServiceContext>> configure)
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

    internal WatchViewModelBuilder WithPlaybackStateStorage(Action<Mock<IResumePlaybackService>> configure)
    {
        configure(_playbackStateStorageMock);
        return this;
    }

    internal WatchViewModelBuilder WithDiscordRpc(Action<Mock<IDiscordRichPresense>> configure)
    {
        configure(_discordRpcMock);
        return this;
    }

    internal WatchViewModelBuilder WithAnimeService(Action<Mock<IAnimeServiceContext>> configure)
    {
        configure(_animeServiceMock);
        return this;
    }

    internal WatchViewModelBuilder WithTimeStampService(Action<Mock<ITimestampsService>> configure)
    {
        configure(_timestampsServiceMock);
        return this;
    }

    internal void VerifyDiscordRpc(Action<Mock<IDiscordRichPresense>> verify) => verify(_discordRpcMock);
    internal void VerifyPlaybackStateStorage(Action<Mock<IResumePlaybackService>> verify) => verify(_playbackStateStorageMock);
    internal void VerifyTrackingService(Action<Mock<ITrackingServiceContext>> verify) => verify(_trackingServiceMock);

}
