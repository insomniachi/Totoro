using Totoro.Core.ViewModels;

namespace Totoro.Core.Tests.Builders;

internal class UserListViewModelBuilder
{

    private readonly Mock<ITrackingServiceContext> _trackingServiceMock = new();
    private readonly Mock<IAnimeServiceContext> _animeServiceMock = new();
    private readonly Mock<IViewService> _viewServiceMock = new();
    private readonly Mock<IConnectivityService> _connectivityServiceMock = new();

    internal UserListViewModel Build()
    {
        return new UserListViewModel(_trackingServiceMock.Object,
                                     _animeServiceMock.Object,
                                     _viewServiceMock.Object,
                                     _connectivityServiceMock.Object);
    }

    internal UserListViewModelBuilder WithTrackingService(Action<Mock<ITrackingServiceContext>> configure)
    {
        configure(_trackingServiceMock);
        return this;
    }

    internal UserListViewModelBuilder WithAnimeService(Action<Mock<IAnimeServiceContext>> configure)
    {
        configure(_animeServiceMock);
        return this;
    }

    internal Mock<IViewService> GetViewServiceMock() => _viewServiceMock;
    internal Mock<IAnimeServiceContext> GetAnimeServiceMock() => _animeServiceMock;
}
