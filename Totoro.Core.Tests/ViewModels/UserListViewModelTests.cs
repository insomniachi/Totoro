using System.Reactive.Linq;
using Totoro.Core.Tests.Builders;
using Totoro.Core.Tests.Helpers;

namespace Totoro.Core.Tests.ViewModels;

public class UserListViewModelTests
{
    [Fact]
    public void UserListViewModel_InitializesProperly()
    {
        // arrange
        var vm = new UserListViewModelBuilder()
            .WithTrackingService(mock =>
            {
                mock.Setup(x => x.GetAnime())
                    .Returns(Observable.Return(SnapshotService.GetSnapshot<AnimeModel[]>("UserAnime")));
            })
            .Build();

        // act
        vm.SetInitialState();

        // assert
        Assert.Equal(AnimeStatus.Watching, vm.Filter.ListStatus);
        Assert.Equal(2, vm.Anime.Count);
    }

    [Theory]
    [InlineData(AnimeStatus.Watching, 2)]
    [InlineData(AnimeStatus.Completed, 2)]
    [InlineData(AnimeStatus.OnHold, 2)]
    [InlineData(AnimeStatus.PlanToWatch, 1)]
    [InlineData(AnimeStatus.Dropped, 1)]
    public void UserListViewModel_FilterByStatusWorks(AnimeStatus status, int count)
    {
        // arrange
        var vm = new UserListViewModelBuilder()
            .WithTrackingService(mock =>
            {
                mock.Setup(x => x.GetAnime())
                    .Returns(Observable.Return(SnapshotService.GetSnapshot<AnimeModel[]>("UserAnime")));
            })
            .Build();

        vm.SetInitialState();

        // act
        vm.ChangeCurrentViewCommand.Execute(status);
        Assert.Equal(count, vm.Anime.Count);
        Assert.Equal(status, vm.Filter.ListStatus);
    }

    [Fact]
    public void UserListViewModel_FilterByNameWorks()
    {
        // arrange
        var vm = new UserListViewModelBuilder()
            .WithTrackingService(mock =>
            {
                mock.Setup(x => x.GetAnime())
                    .Returns(Observable.Return(SnapshotService.GetSnapshot<AnimeModel[]>("UserAnime")));
            })
            .Build();

        vm.SetInitialState();

        // act
        vm.SearchText = @"Chainsaw Man";

        // assert
        Assert.Single(vm.Anime);
        Assert.Equal(@"Chainsaw Man", vm.Anime[0].Title);
    }

    [Fact]
    public async void UserListViewModel_SearchingInQuickAddGivesResults()
    {
        // arrage
        var vm = new UserListViewModelBuilder()
            .WithAnimeService(mock =>
            {
                mock.Setup(x => x.GetAnime(It.IsAny<string>()))
                    .Returns((string text) =>
                    {
                        return Observable.Return(new AnimeModel[] { new AnimeModel { Title = text, Id = 10 } });
                    });
            })
            .Build();

        // act
        vm.QuickAddSearchText = "Chainsaw Man";
        await Task.Delay(200);


        // assert;
        Assert.Single(vm.QuickSearchResults);
        Assert.Equal("Chainsaw Man", vm.QuickSearchResults[0].Title);
    }

    [Fact]
    public async void UserListViewModel_SearchingInQuickAddWithLessThan3Length()
    {
        // arrage
        var vmBuilder = new UserListViewModelBuilder()
            .WithAnimeService(mock =>
            {
                mock.Setup(x => x.GetAnime(It.IsAny<string>()))
                    .Returns((string text) =>
                    {
                        return Observable.Return(new AnimeModel[] { new AnimeModel { Title = text, Id = 10 } });
                    });
            });
        var vm = vmBuilder.Build();

        // act
        vm.QuickAddSearchText = "Cha";
        await Task.Delay(200);

        // assert;
        vmBuilder.GetAnimeServiceMock().Verify(x => x.GetAnime(It.IsAny<string>()), Times.Never);
    }
}
