using AnimDL.Api;
using Totoro.Core.ViewModels;

namespace Totoro.Core.Tests.Builders;

public class WatchViewModelBuilder
{
    private IProviderFactory _providerFactory = Mock.Of<IProviderFactory>();
    private ITrackingService _trackingService = Mock.Of<ITrackingService>();
    private IViewService _viewService = Mock.Of<IViewService>();
    private ISettings _settings = Mock.Of<ISettings>();
    private IPlaybackStateStorage _playbackStateStorage = Mock.Of<IPlaybackStateStorage>();
    private IDiscordRichPresense _discordRpc = Mock.Of<IDiscordRichPresense>();
    private IAnimeService _animeService = Mock.Of<IAnimeService>();
    private IMediaPlayer _mediaPlayer = Mock.Of<IMediaPlayer>();
    private Mock<IDiscordRichPresense> _discordRpcMock;
    private ITimestampsService _timestampsService = Mock.Of<ITimestampsService>();

    public WatchViewModel Bulid()
    {
        return new WatchViewModel(_providerFactory,
                                  _trackingService,
                                  _viewService,
                                  _settings,
                                  _playbackStateStorage,
                                  _discordRpc,
                                  _animeService,
                                  _mediaPlayer,
                                  _timestampsService);
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
        var mock = new Mock<ITrackingService>();
        configure(mock);
        _trackingService = mock.Object;
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
        var mock = new Mock<IPlaybackStateStorage>();
        configure(mock);
        _playbackStateStorage = mock.Object;
        return this;
    }

    public WatchViewModelBuilder WithDiscordRpc(Action<Mock<IDiscordRichPresense>> configure)
    {
        _discordRpcMock = new Mock<IDiscordRichPresense>();
        configure(_discordRpcMock);
        _discordRpc = _discordRpcMock.Object;
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

    public void Verify(Action<Mock<IDiscordRichPresense>> verify) => verify(_discordRpcMock);

}
