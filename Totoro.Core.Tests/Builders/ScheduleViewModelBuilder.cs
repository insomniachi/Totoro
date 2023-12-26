using Totoro.Core.ViewModels;

namespace Totoro.Core.Tests.Builders;

internal class ScheduleViewModelBuilder
{
    private readonly Mock<ITrackingServiceContext> _trackingServiceMock = new();

    internal ScheduleViewModel Build()
    {
        return new ScheduleViewModel(_trackingServiceMock.Object);
    }

    internal ScheduleViewModelBuilder WithTrackingService(Action<Mock<ITrackingServiceContext>> configure)
    {
        configure(_trackingServiceMock);
        return this;
    }
}
