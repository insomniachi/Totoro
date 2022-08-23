using AnimDL.Api;
using AnimDL.UI.Core.ViewModels;

namespace AnimDL.WinUI.Tests.Builders;

public class WatchViewModelBuilder
{
    private IProviderFactory _providerFactory = Mock.Of<IProviderFactory>();
    private ITrackingService _trackingService = Mock.Of<ITrackingService>();
    private IViewService _viewService = Mock.Of<IViewService>();
    private ISettings _settings = Mock.Of<ISettings>();
    private IPlaybackStateStorage _playbackStateStorage = Mock.Of<IPlaybackStateStorage>();
    private IDiscordRichPresense _discordRpc = Mock.Of<IDiscordRichPresense>();
    private Mock<IDiscordRichPresense> _discordRpcMock;
    
    public WatchViewModel Bulid()
    {
        return new WatchViewModel(_providerFactory,
                                  _trackingService,
                                  _viewService,
                                  _settings,
                                  _playbackStateStorage,
                                  _discordRpc,
                                  MessageBus.Current);
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

    public void Verify(Action<Mock<IDiscordRichPresense>> verify) => verify(_discordRpcMock);

}
