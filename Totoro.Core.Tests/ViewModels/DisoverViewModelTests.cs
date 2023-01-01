using System.Reactive.Linq;
using Totoro.Core.Tests.Builders;
using Totoro.Core.Tests.Helpers;
using Totoro.Core.ViewModels;

namespace Totoro.Core.Tests.ViewModels;

public class DisoverViewModelTests
{

    [Fact]
    public async void DiscoverViewModel_Initializes()
    {
        var vmBuilder = new DiscoverViewModelBuilder()
            .WithSettings(mock => mock.Setup(x => x.DefaultProviderType).Returns(ProviderType.GogoAnime))
            .WithProviderFacotry(mock =>
            {
                var providerMock = new Mock<IProvider>();
                var airedEpisodesProviderMock = new Mock<IAiredEpisodeProvider>();
                airedEpisodesProviderMock.Setup(x => x.GetRecentlyAiredEpisodes(It.IsAny<int>())).Returns(Task.FromResult(new List<AiredEpisode>
                {
                    new TestAiredEpisode(),new TestAiredEpisode(),new TestAiredEpisode()
                } as IEnumerable<AiredEpisode>));
                providerMock.Setup(x => x.AiredEpisodesProvider).Returns(airedEpisodesProviderMock.Object);
                mock.Setup(x => x.GetProvider(ProviderType.GogoAnime)).Returns(providerMock.Object);
            })
            .WithTrackingService(mock => mock.Setup(x => x.GetAnime()).Returns(Observable.Return(Enumerable.Empty<AnimeModel>())))
            .WithFeaturedAnimeProvider(mock => mock.Setup(x => x.GetFeaturedAnime()).Returns(Observable.Return(Enumerable.Repeat(new FeaturedAnime { Title = "Hyouka" }, 5))));
        var vm = vmBuilder.Build();

        await vm.OnNavigatedTo(parameters: new Dictionary<string, object>());

        Assert.Equal(3, vm.Episodes.Count);
    }

    [Fact]
    public void DisoverViewModel_ClickingEpisodes_GoesToWatch()
    {
        var vmBuilder = new DiscoverViewModelBuilder();
        var vm = vmBuilder.Build();

        vm.SelectEpisode.Execute(new TestAiredEpisode { Title = "Hyouka" });

        vmBuilder.GetNavigationService().Verify(x => x.NavigateTo(It.IsAny<WatchViewModel>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async void DiscoverViewModel_SearchingForAnimeWorks()
    {
        var vmBuilder = new DiscoverViewModelBuilder();
        var vm = vmBuilder
            .WithSettings(mock => mock.Setup(x => x.DefaultProviderType).Returns(ProviderType.GogoAnime))
            .WithProviderFacotry(mock =>
            {
                var providerMock = new Mock<IProvider>();
                var catalogMock = new Mock<ICatalog>();
                catalogMock.Setup(x => x.Search(It.IsAny<string>())).Returns(AsyncEnumerable.Range(1, 3).Select(x =>
                    new SearchResult
                    {
                        Title = x.ToString(),
                        Url = x.ToString(),
                    }));
                providerMock.Setup(x => x.Catalog).Returns(catalogMock.Object);
                mock.Setup(x => x.GetProvider(ProviderType.GogoAnime)).Returns(providerMock.Object);
            })
            .Build();

        vm.SearchText = "Hyouka";

        await Task.Delay(300);

        Assert.Equal(3, vm.AnimeSearchResults.Count);
    }

    [Fact]
    public void DisoverViewModel_SelectingAnimeGoesToWatchViewModel()
    {
        var vmBuilder = new DiscoverViewModelBuilder();
        var vm = vmBuilder.Build();

        vm.SelectSearchResult.Execute(new SearchResult { Title = "Hyouka" });

        vmBuilder.GetNavigationService().Verify(x => x.NavigateTo(It.IsAny<WatchViewModel>(), It.IsAny<Dictionary<string, object>>(), It.IsAny<bool>()), Times.Once);
    }
}
