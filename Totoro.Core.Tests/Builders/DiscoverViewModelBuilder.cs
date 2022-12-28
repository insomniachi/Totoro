using Totoro.Core.Services;
using Totoro.Core.ViewModels;

namespace Totoro.Core.Tests.Builders;

internal class DiscoverViewModelBuilder
{
    private readonly Mock<IRecentEpisodesProvider> _recentEpisodesProviderMock = new();
    private readonly Mock<IFeaturedAnimeProvider> _featuredAnimeProviderMock = new();
    private readonly Mock<INavigationService> _navigationServiceMock = new();
    private readonly Mock<ITrackingService> _trackingServiceMock = new();
    private Mock<ISchedulerProvider> _schedulerProvider;


    internal DiscoverViewModel Builder()
    {
        return new DiscoverViewModel(Mock.Of<IProviderFactory>(),
                                     Mock.Of<ISettings>(),
                                     _navigationServiceMock.Object,
                                     _trackingServiceMock.Object,
                                     _schedulerProvider?.Object ?? new SchedulerProvider());
    }

    internal DiscoverViewModelBuilder WithNavigationService(Action<Mock<INavigationService>> configure)
    {
        configure(_navigationServiceMock);
        return this;
    }

    internal DiscoverViewModelBuilder WithFeaturedAnimeProvider(Action<Mock<IFeaturedAnimeProvider>> configure)
    {
        configure(_featuredAnimeProviderMock);
        return this;
    }

    internal DiscoverViewModelBuilder WithScheduler(Action<Mock<ISchedulerProvider>> configure)
    {
        _schedulerProvider = new();
        configure(_schedulerProvider);
        return this;
    }

    internal DiscoverViewModelBuilder WithTrackingService(Action<Mock<ITrackingService>> configure)
    {
        configure(_trackingServiceMock);
        return this;
    }

    internal DiscoverViewModelBuilder WithRecentEpisodesProvider(Action<Mock<IRecentEpisodesProvider>> configure)
    {
        configure(_recentEpisodesProviderMock);
        return this;
    }

    internal Mock<INavigationService> GetNavigationService() => _navigationServiceMock;
}
