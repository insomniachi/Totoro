using System.Reactive.Linq;
using Totoro.Core.Tests.Builders;
using Totoro.Core.Tests.Helpers;

namespace Totoro.Core.Tests.ViewModels;

public class ScheduleViewModelTests
{
    [Fact]
    public void ScheduleViewModel_InitializesCorrectly()
    {
        var vm = BaseViewModelBuilder().Build();
        vm.SetInitialState();

        Assert.Equal(0, vm.Schedule[DayOfWeek.Monday].Count);
        Assert.Equal(1, vm.Schedule[DayOfWeek.Tuesday].Count);
        Assert.Equal(1, vm.Schedule[DayOfWeek.Wednesday].Count);
        Assert.Equal(2, vm.Schedule[DayOfWeek.Thursday].Count);
        Assert.Equal(0, vm.Schedule[DayOfWeek.Friday].Count);
        Assert.Equal(2, vm.Schedule[DayOfWeek.Saturday].Count);
        Assert.Equal(1, vm.Schedule[DayOfWeek.Sunday].Count);

        Assert.Null(vm.WeeklySchedule.FirstOrDefault(x => x.DayOfWeek == DayOfWeek.Monday));
        Assert.Null(vm.WeeklySchedule.FirstOrDefault(x => x.DayOfWeek == DayOfWeek.Friday));
    }

    [Fact]
    public void ScheduleViewModel_FilteryByDayWorks()
    {
        var vm = BaseViewModelBuilder().Build();
        vm.SetInitialState();

        vm.SelectedDay = vm.Schedule[DayOfWeek.Tuesday];
        Assert.Single(vm.Anime);
        Assert.All(vm.Anime, x => Assert.Equal(DayOfWeek.Tuesday, x.BroadcastDay));

        vm.SelectedDay = vm.Schedule[DayOfWeek.Wednesday];
        Assert.Single(vm.Anime);
        Assert.All(vm.Anime, x => Assert.Equal(DayOfWeek.Wednesday, x.BroadcastDay));

        vm.SelectedDay = vm.Schedule[DayOfWeek.Thursday];
        Assert.Equal(2, vm.Anime.Count);
        Assert.All(vm.Anime, x => Assert.Equal(DayOfWeek.Thursday, x.BroadcastDay));

        vm.SelectedDay = vm.Schedule[DayOfWeek.Saturday];
        Assert.Equal(2, vm.Anime.Count);
        Assert.All(vm.Anime, x => Assert.Equal(DayOfWeek.Saturday, x.BroadcastDay));

        vm.SelectedDay = vm.Schedule[DayOfWeek.Sunday];
        Assert.Single(vm.Anime);
        Assert.All(vm.Anime, x => Assert.Equal(DayOfWeek.Sunday, x.BroadcastDay));
    }

    [Theory]
    [InlineData(5, DayOfWeek.Tuesday)]  // No show in Monday, chose the next day
    [InlineData(6, DayOfWeek.Tuesday)]
    [InlineData(7, DayOfWeek.Wednesday)]
    [InlineData(8, DayOfWeek.Thursday)]
    [InlineData(9, DayOfWeek.Tuesday)]  // No show in Friday, chose next day
    [InlineData(10, DayOfWeek.Tuesday)] // No show in Saturday, chose next day
    [InlineData(11, DayOfWeek.Tuesday)] // No show in Sunday, chose next day
    public void ScheduleViewModel_AutoSelectsCorrectDay(int day, DayOfWeek expected)
    {
        // arrange
        var vm = new ScheduleViewModelBuilder()
            .WithTrackingService(mock =>
            {
                // Same as other snapshot, but shows from sunday, and saturday are removed.
                mock.Setup(x => x.GetCurrentlyAiringTrackedAnime()).Returns(Observable.Return(SnapshotService.GetSnapshot<AnimeModel[]>("CurrentlyAiringTrackedAnime2")));
            })
            .WithSystemClock(mock =>
            {
                mock.Setup(x => x.Today).Returns(new DateTime(2022, 12, day));
            })
            .Build();

        // act
        vm.SetInitialState();

        // assert
        Assert.Equal(expected, vm.SelectedDay.DayOfWeek);
    }

    [Fact]
    public void ScheduleViewModel_WhenWatchListEmpty()
    {
        // arrage
        var vm = new ScheduleViewModelBuilder()
            .WithTrackingService(mock =>
            {
                mock.Setup(x => x.GetCurrentlyAiringTrackedAnime()).Returns(Observable.Return(Enumerable.Empty<AnimeModel>()));
            })
            .Build();

        // act
        vm.SetInitialState();

        // assert
        Assert.Null(vm.SelectedDay);
        Assert.Empty(vm.WeeklySchedule);
        Assert.Empty(vm.Anime);
    }

    private static ScheduleViewModelBuilder BaseViewModelBuilder()
    {
        return new ScheduleViewModelBuilder()
            .WithTrackingService(mock =>
            {
                mock.Setup(x => x.GetCurrentlyAiringTrackedAnime()).Returns(Observable.Return(SnapshotService.GetSnapshot<AnimeModel[]>("CurrentlyAiringTrackedAnime")));
            });
    }
}
