using System.Reactive.Linq;
using Microsoft.Reactive.Testing;
using ReactiveUI.Testing;
using Totoro.Core.Tests.Builders;
using Totoro.Core.ViewModels;

namespace Totoro.Core.Tests.ViewModels;

public class DisoverViewModelTests
{

    [Fact]
    public async void DiscoverViewModel_Initializes()
    {
        var vmBuilder = new DiscoverViewModelBuilder()
            .WithRecentEpisodesProvider(mock => mock.Setup(x => x.GetRecentlyAiredEpisodes()).Returns(Observable.Return(new List<AiredEpisode>
            {
                new AiredEpisode(){Anime = "A"}, new AiredEpisode{Anime = "B" }, new AiredEpisode{Anime="C"}
            })))
            .WithTrackingService(mock => mock.Setup(x => x.GetAnime()).Returns(Observable.Return(Enumerable.Empty<AnimeModel>())))
            .WithFeaturedAnimeProvider(mock => mock.Setup(x => x.GetFeaturedAnime()).Returns(Observable.Return(Enumerable.Repeat(new FeaturedAnime { Title = "Hyouka" }, 5))));
        var vm = vmBuilder.Builder();

        await vm.OnNavigatedTo(parameters: new Dictionary<string, object>());
        await vm.SetInitialState();

        vm.ShowOnlyWatchingAnime = false;

        Assert.Equal(3, vm.Episodes.Count);
        //Assert.Equal(5, vm.Featured.Count);
    }

    [Fact]
    public void DisoverViewModel_ClickingEpisodes_GoesToWatch()
    {
        var vmBuilder = new DiscoverViewModelBuilder();
        var vm = vmBuilder.Builder();

        vm.SelectEpisode.Execute(new AiredEpisode { Anime = "Hyouka" });

        vmBuilder.GetNavigationService().Verify(x => x.NavigateTo(It.IsAny<WatchViewModel>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public void DisoverViewModel_ClickingFeatured_GoesToWatch()
    {
        var vmBuilder = new DiscoverViewModelBuilder()
            .WithFeaturedAnimeProvider(mock =>
            {
                mock.Setup(x => x.GetFeaturedAnime()).Returns(Observable.Return(Enumerable.Repeat(new FeaturedAnime{Title = "Hyouka"}, 5)));
            });
        var vm = vmBuilder.Builder();

        vm.SelectFeaturedAnime.Execute(new FeaturedAnime { Title = "Hyouka", Url = "anime/12189/" });

        vmBuilder.GetNavigationService().Verify(x => x.NavigateTo(It.IsAny<WatchViewModel>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<bool>()), Times.Once);
    }

    [Theory]
    [InlineData(5)]
    public async void DiscoverViewModel_DisplayedFeaturedAnime_Changes(int count)
    {
        // arrange
        var scheduler = new TestScheduler();
        var vmBuilder = new DiscoverViewModelBuilder()
            .WithTrackingService(mock =>
            {
                mock.Setup(x => x.GetAnime()).Returns(Observable.Return(Enumerable.Empty<AnimeModel>()));
            })
            .WithFeaturedAnimeProvider(mock =>
            {
                mock.Setup(x => x.GetFeaturedAnime()).Returns(Observable.Return(Enumerable.Repeat(new FeaturedAnime { Title = "Hyouka" }, count)));
            })
            .WithScheduler(mock =>
            {
                mock.Setup(x => x.MainThreadScheduler).Returns(scheduler);
                mock.Setup(x => x.TaskpoolScheduler).Returns(scheduler);
            });
        var vm = vmBuilder.Builder();
        await vm.SetInitialState();
        Assert.Equal(0, vm.SelectedIndex);

        for (int i = 0; i < count; i++)
        {
            scheduler.AdvanceBy(scheduler.FromTimeSpan(TimeSpan.FromSeconds(11)));

            if(i == count -1)
            {
                Assert.Equal(0, vm.SelectedIndex);
            }
            else
            {
                Assert.Equal(i + 1, vm.SelectedIndex);
            }
        }

    }

    [Fact]
    public async void DiscoverViewModel_DisplayedFeaturedAnime_DoesNotChangeIfNone()
    {
        // arrange
        var scheduler = new TestScheduler();
        var vmBuilder = new DiscoverViewModelBuilder()
            .WithTrackingService(mock =>
            {
                mock.Setup(x => x.GetAnime()).Returns(Observable.Return(Enumerable.Empty<AnimeModel>()));
            })
            .WithFeaturedAnimeProvider(mock =>
            {
                mock.Setup(x => x.GetFeaturedAnime()).Returns(Observable.Return(Enumerable.Empty<FeaturedAnime>()));
            })
            .WithScheduler(mock =>
            {
                mock.Setup(x => x.MainThreadScheduler).Returns(scheduler);
                mock.Setup(x => x.TaskpoolScheduler).Returns(scheduler);
            });
        var vm = vmBuilder.Builder();
        await vm.SetInitialState();
        Assert.Equal(0, vm.SelectedIndex);

        scheduler.AdvanceBy(scheduler.FromTimeSpan(TimeSpan.FromSeconds(11)));
        Assert.Equal(0, vm.SelectedIndex);  
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DiscoverViewModel_ShowUserAnimeDisableStatus_WithAuthenticationStatus(bool isAuthenticated)
    {
        var vm = new DiscoverViewModelBuilder()
            .WithTrackingService(mock => mock.Setup(x => x.IsAuthenticated).Returns(isAuthenticated))
            .Builder();

        Assert.Equal(isAuthenticated, vm.ShowOnlyWatchingAnime);
    }
}
