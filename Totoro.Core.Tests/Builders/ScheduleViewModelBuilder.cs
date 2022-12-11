using Totoro.Core.Services;
using Totoro.Core.ViewModels;

namespace Totoro.Core.Tests.Builders;

internal class ScheduleViewModelBuilder
{
    private readonly Mock<ITrackingService> _trackingServiceMock = new();
    private Mock<ISystemClock> _systemClockMock;

    internal ScheduleViewModel Build()
    {
        return new ScheduleViewModel(_trackingServiceMock.Object,
                                     _systemClockMock?.Object ?? new SystemClock());
    }

    internal ScheduleViewModelBuilder WithTrackingService(Action<Mock<ITrackingService>> configure)
    {
        configure(_trackingServiceMock);
        return this;
    }

    internal ScheduleViewModelBuilder WithSystemClock(Action<Mock<ISystemClock>> configure)
    {
        _systemClockMock = new();
        configure(_systemClockMock);
        return this;
    }
}
