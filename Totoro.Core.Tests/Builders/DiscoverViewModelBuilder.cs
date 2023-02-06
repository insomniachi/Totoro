using Totoro.Core.ViewModels;

namespace Totoro.Core.Tests.Builders;

internal class DiscoverViewModelBuilder
{
    private readonly Mock<IFeaturedAnimeProvider> _featuredAnimeProviderMock = new();
    private readonly Mock<INavigationService> _navigationServiceMock = new();
    private readonly Mock<ITrackingService> _trackingServiceMock = new();
    private Mock<ISchedulerProvider> _schedulerProvider;
    private readonly Mock<IProviderFactory> _providerFactoryMock = new();
    private readonly Mock<ISettings> _settingsMock = new();


    internal DiscoverViewModel Build()
    {
        return new DiscoverViewModel(_providerFactoryMock.Object,
                                     _settingsMock.Object,
                                     _navigationServiceMock.Object);
    }

    internal DiscoverViewModelBuilder WithSettings(Action<Mock<ISettings>> configure)
    {
        configure(_settingsMock);
        return this;
    }

    internal DiscoverViewModelBuilder WithProviderFacotry(Action<Mock<IProviderFactory>> configure)
    {
        configure(_providerFactoryMock);
        return this;
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

    internal Mock<INavigationService> GetNavigationService() => _navigationServiceMock;
}
