using System.Linq.Expressions;
using Moq;
using Totoro.Core.ViewModels;

namespace Totoro.Core.Tests.Builders;

internal class UserListViewModelBuilder
{

    private readonly Mock<ITrackingService> _trackingServiceMock = new();
    private readonly Mock<INavigationService> _navigationServiceMock = new();
    private readonly Mock<IAnimeService> _animeServiceMock = new();
    private readonly Mock<IViewService> _viewServiceMock = new();

    internal UserListViewModel Build()
    {
        return new UserListViewModel(_trackingServiceMock.Object,
                                     _navigationServiceMock.Object,
                                     _animeServiceMock.Object,
                                     _viewServiceMock.Object);
    }

    internal UserListViewModelBuilder WithTrackingService(Action<Mock<ITrackingService>> configure)
    {
        configure(_trackingServiceMock);
        return this;
    }

    internal UserListViewModelBuilder WithAnimeService(Action<Mock<IAnimeService>> configure)
    {
        configure(_animeServiceMock);
        return this;
    }

    internal Mock<IViewService> GetViewServiceMock() => _viewServiceMock;
    internal Mock<INavigationService> GetNavigationServiceMock() => _navigationServiceMock;
    internal Mock<IAnimeService> GetAnimeServiceMock() => _animeServiceMock;
}
