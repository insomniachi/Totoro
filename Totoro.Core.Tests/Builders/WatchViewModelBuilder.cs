using AnimDL.Api;
using Totoro.Core.Tests.Helpers;
using Totoro.Core.ViewModels;

namespace Totoro.Core.Tests.Builders;

internal class WatchViewModelBuilder
{
    private IProviderFactory _providerFactory = Mock.Of<IProviderFactory>();
    private ITrackingService GetTrackingServce() => _trackingServiceMock.Object;
    private IViewService _viewService = Mock.Of<IViewService>();
    private ISettings _settings = Mock.Of<ISettings>();
    private IPlaybackStateStorage GetPlaybackStateStorate() => _playbackStateStorageMock.Object;
    private IDiscordRichPresense GetDiscordRpc() => _discordRpcMock.Object;
    private IAnimeService _animeService = Mock.Of<IAnimeService>();
    private IMediaPlayer _mediaPlayer;
    private ITimestampsService _timestampsService = Mock.Of<ITimestampsService>();
    private IRecentEpisodesProvider _recentEpisodesProvider = Mock.Of<IRecentEpisodesProvider>();

    private readonly Mock<IDiscordRichPresense> _discordRpcMock = new();
    private readonly Mock<IPlaybackStateStorage> _playbackStateStorageMock = new();
    private readonly Mock<ITrackingService> _trackingServiceMock = new();

    public MockMediaPlayer MediaPlayer { get; } = new();

    public WatchViewModelBuilder()
    {
        _mediaPlayer = MediaPlayer;
    }

    public WatchViewModel Bulid()
    {
        return new WatchViewModel(_providerFactory,
                                  GetTrackingServce(),
                                  _viewService,
                                  _settings,
                                  GetPlaybackStateStorate(),
                                  GetDiscordRpc(),
                                  _animeService,
                                  _mediaPlayer,
                                  _timestampsService,
                                  _recentEpisodesProvider,
                                  Mock.Of<ILocalMediaService>());
    }

    public WatchViewModelBuilder WithProviderFactory(Action<Mock<IProviderFactory>> configure)
    {
        var mock = new Mock<IProviderFactory>();
        configure(mock);
        _providerFactory = mock.Object;
        return this;
    }

    public WatchViewModelBuilder WithTrackingService(Action<Mock<ITrackingService>> configure)
    {
        configure(_trackingServiceMock);
        return this;
    }

    public WatchViewModelBuilder WithViewService(Action<Mock<IViewService>> configure)
    {
        var mock = new Mock<IViewService>();
        configure(mock);
        _viewService = mock.Object;
        return this;
    }

    public WatchViewModelBuilder WithSettings(Action<Mock<ISettings>> configure)
    {
        var mock = new Mock<ISettings>();
        configure(mock);
        _settings = mock.Object;
        return this;
    }

    public WatchViewModelBuilder WithPlaybackStateStorage(Action<Mock<IPlaybackStateStorage>> configure)
    {
        configure(_playbackStateStorageMock);
        return this;
    }

    public WatchViewModelBuilder WithDiscordRpc(Action<Mock<IDiscordRichPresense>> configure)
    {
        configure(_discordRpcMock);
        return this;
    }

    public WatchViewModelBuilder WithAnimeService(Action<Mock<IAnimeService>> configure)
    {
        var mock = new Mock<IAnimeService>();
        configure(mock);
        _animeService = mock.Object;
        return this;
    }

    public WatchViewModelBuilder WithMediaPlayer(Action<Mock<IMediaPlayer>> configure)
    {
        var mock = new Mock<IMediaPlayer>();
        configure(mock);
        _mediaPlayer = mock.Object;
        return this;
    }

    public WatchViewModelBuilder WithTimeStampService(Action<Mock<ITimestampsService>> configure)
    {
        var mock = new Mock<ITimestampsService>();
        configure(mock);
        _timestampsService = mock.Object;
        return this;
    }

    public WatchViewModelBuilder WithRecentEpisodesProvider(Action<Mock<IRecentEpisodesProvider>> configure)
    {
        var mock = new Mock<IRecentEpisodesProvider>();
        configure(mock);
        _recentEpisodesProvider = mock.Object;
        return this;
    }

    public void VerifyDiscordRpc(Action<Mock<IDiscordRichPresense>> verify) => verify(_discordRpcMock);
    public void VerifyPlaybackStateStorage(Action<Mock<IPlaybackStateStorage>> verify) => verify(_playbackStateStorageMock);
    public void VerifyTrackingService(Action<Mock<ITrackingService>> verify) => verify(_trackingServiceMock);

}
