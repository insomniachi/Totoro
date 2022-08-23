using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AnimDL.Api;
using AnimDL.UI.Tests.Helpers;
using AnimDL.WinUI.Tests.Builders;

namespace AnimDL.WinUI.Tests.ViewModels;

public class WatchViewModelTests
{
    [Theory]
    [InlineData(24, 0)]
    [InlineData(24, 10)]
    [InlineData(24, 23)]
    public async Task WatchViewModel_AfterNavigation_PlaysFirstUnwatchedEpisode(int totalEpisodes, int lastEpisodeCompleted)
    {
        // arrange
        var result = new SearchResult { Title = "Hyouka", Url = "hyoukapageurl" };
        var provider = GetProvider(result, totalEpisodes);

        var vm = BaseViewModel(provider).Bulid();
        var anime = new AnimeModel
        {
            Title = "Hyouka",
            Id = 10000,
            TotalEpisodes = totalEpisodes,
            Tracking = new Tracking
            {
                WatchedEpisodes = lastEpisodeCompleted,
                Status = AnimeStatus.Watching
            }
        };

        // act
        await vm.OnNavigatedTo(new Dictionary<string, object>
        {
            ["Anime"] = anime
        });

        await Task.Delay(10);

        // assert
        Assert.Equal(totalEpisodes, vm.Episodes.Last());
        Assert.Equal(anime.Tracking.WatchedEpisodes + 1, vm.CurrentEpisode);
        Assert.Equal($"{result.Url}_stream_{vm.CurrentEpisode}", vm.Url);
    }

    [Fact]
    public async Task WatchViewModel_AfterNavigation_PlaysNothingIfNoNewEpisodes()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult { Title = "Hyouka", Url = "hyoukapageurl" };
        var provider = GetProvider(result, ep);

        var vm = BaseViewModel(provider).Bulid();
        var anime = new AnimeModel
        {
            Title = "Hyouka",
            Id = 10000,
            TotalEpisodes = ep,
            Tracking = new Tracking
            {
                WatchedEpisodes = ep,
                Status = AnimeStatus.Watching
            }
        };

        // act
        await vm.OnNavigatedTo(new Dictionary<string, object>
        {
            ["Anime"] = anime
        });

        await Task.Delay(10);

        // assert
        Assert.Equal(ep, vm.Episodes.Last());
        Assert.Null(vm.CurrentEpisode);
        Assert.True(string.IsNullOrEmpty(vm.Url));
    }

    [Fact]
    public async Task WatchViewModel_AfterNavigation_UntrackedAnimeStartsFromFirstEpisode()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult { Title = "Hyouka", Url = "hyoukapageurl" };
        var provider = GetProvider(result, ep);

        var vm = BaseViewModel(provider).Bulid();
        var anime = new AnimeModel
        {
            Title = "Hyouka",
            Id = 10000,
            TotalEpisodes = ep,
        };

        // act
        await vm.OnNavigatedTo(new Dictionary<string, object>
        {
            ["Anime"] = anime
        });

        await Task.Delay(10);

        // assert
        Assert.Equal(ep, vm.Episodes.Last());
        Assert.Equal(1, vm.CurrentEpisode);
        Assert.Equal($"{result.Url}_stream_{vm.CurrentEpisode}", vm.Url);
    }

    [Fact]
    public async Task WatchViewModel_Search_SelectingSuggestionPopulatesEpisodes()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult { Title = "Hyouka", Url = "hyoukapageurl" };
        var provider = GetProvider(result, ep);

        var vm = BaseViewModel(provider).Bulid();

        // act
        vm.SearchResultPicked.Execute(result);
        
        await Task.Delay(10);
        
        // assert
        Assert.Equal(ep, vm.Episodes.Count);
        Assert.Null(vm.CurrentEpisode);
    }

    [Fact]
    public async Task WatchViewModel_ChoosingEpisode_PlaysThatEpisode()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult { Title = "Hyouka", Url = "hyoukapageurl" };
        var provider = GetProvider(result, ep);

        var vm = BaseViewModel(provider).Bulid();

        // act
        vm.SearchResultPicked.Execute(result);
        await Task.Delay(10);
        vm.CurrentEpisode = 10;
        await Task.Delay(10);

        // assert
        Assert.Equal($"{result.Url}_stream_{vm.CurrentEpisode}", vm.Url);
    }

    [Fact]
    public async Task WatchViewModel_SendsCommandToPlayVideoFromLastPostionWhenVideoLoaded()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult { Title = "Hyouka", Url = "hyoukapageurl" };
        var provider = GetProvider(result, ep);

        var time = 100.0;
        var vm = BaseViewModel(provider).WithPlaybackStateStorage(x =>
                    {
                        x.Setup(x => x.GetTime(It.IsAny<long>(), It.IsAny<int>())).Returns(time);
                    })
                    .Bulid();

        // act
        vm.SearchResultPicked.Execute(result);
        await Task.Delay(10);
        vm.CurrentEpisode = 10;
        await Task.Delay(10);

        Assert.True(string.IsNullOrEmpty(vm.VideoPlayerRequestMessage));
        // video player is loaded and can start playing
        MessageBus.Current.SendMessage(new WebMessage { MessageType = WebMessageType.CanPlay });

        Assert.False(string.IsNullOrEmpty(vm.VideoPlayerRequestMessage));
        var message = JsonNode.Parse(vm.VideoPlayerRequestMessage);
        Assert.Equal(WebMessageType.Play.ToString(), message["MessageType"].ToString());
        Assert.Equal(time.ToString(), message["StartTime"].ToString());
    }

    [Fact]
    public async Task WatchViewModel_DiscordRpcWorks()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult { Title = "Hyouka", Url = "hyoukapageurl" };
        var provider = GetProvider(result, ep);

        var vmBuilder = BaseViewModel(provider)
            .WithDiscordRpc(x => { })
            .WithSettings(x => 
            {
                x.Setup(x => x.UseDiscordRichPresense).Returns(true);
            });
        var vm = vmBuilder.Bulid();

        var anime = new AnimeModel
        {
            Title = "Hyouka",
            Id = 10000,
            TotalEpisodes = ep,
            Tracking = new Tracking
            {
                WatchedEpisodes = 10,
                Status = AnimeStatus.Watching
            }
        };

        await vm.OnNavigatedTo(new Dictionary<string, object>
        {
            ["Anime"] = anime
        });

        await Task.Delay(10);

        MessageBus.Current.SendMessage(new WebMessage
        {
            MessageType = WebMessageType.DurationUpdate, 
            Content = TimeSpan.FromMinutes(24).TotalSeconds.ToString() 
        });
        MessageBus.Current.SendMessage(new WebMessage { MessageType = WebMessageType.TimeUpdate, Content = "0" });

        await Task.Delay(10);

        MessageBus.Current.SendMessage(new WebMessage { MessageType = WebMessageType.CanPlay });

        MessageBus.Current.SendMessage(new WebMessage { MessageType = WebMessageType.Play });

        await Task.Delay(10);

        vmBuilder.Verify(x =>
        {
            x.Verify(x => x.SetPresense(anime, 11, TimeSpan.FromMinutes(24)), Times.Once);
        });

        MessageBus.Current.SendMessage(new WebMessage { MessageType = WebMessageType.TimeUpdate, Content = "60" });
        
        MessageBus.Current.SendMessage(new WebMessage { MessageType = WebMessageType.Pause });

        vmBuilder.Verify(x =>
        {
            x.Verify(x => x.UpdateDetails("Paused"), Times.Once);
            x.Verify(x => x.ClearTimer(), Times.Once);
        });

        MessageBus.Current.SendMessage(new WebMessage { MessageType = WebMessageType.Play });

        vmBuilder.Verify(x =>
        {
            x.Verify(x => x.SetPresense(anime, 11, TimeSpan.FromMinutes(23)), Times.Once);
        });
    }


    private static WatchViewModelBuilder BaseViewModel(IProvider provider) => new WatchViewModelBuilder()
            .WithProviderFactory(factory =>
            {
                factory.Setup(x => x.GetProvider(It.IsAny<ProviderType>())).Returns(provider);
            })
            .WithSettings(settings =>
            {
                settings.Setup(x => x.DefaultProviderType).Returns(ProviderType.AnimixPlay);
                settings.Setup(x => x.UseDiscordRichPresense).Returns(false);
                settings.Setup(x => x.PreferSubs).Returns(false);
                settings.Setup(x => x.ElementTheme).Returns(ElementTheme.Dark);
            });

    private static IProvider GetProvider(SearchResult result, int numberOfEps)
    {
        var catalogMock = new Mock<TestCatalog>();
        catalogMock.Setup(x => x.SearchByMalId(It.IsAny<long>())).Returns(Task.FromResult((result, result)));
        catalogMock.Setup(x => x.Search(It.IsAny<string>())).Returns(RepeatResults(result, 5));

        var streamProviderMock = new Mock<IStreamProvider>();
        streamProviderMock.Setup(x => x.GetNumberOfStreams(It.IsAny<string>())).Returns(Task.FromResult(numberOfEps));
        streamProviderMock.Setup(x => x.GetStreams(It.IsAny<string>(), It.IsAny<System.Range>()))
                          .Returns((string url, System.Range range) =>
                          {
                              return Default(url, numberOfEps, range);
                          });

        var providerMock = new Mock<IProvider>();
        providerMock.Setup(x => x.Catalog).Returns(catalogMock.Object);
        providerMock.Setup(x => x.StreamProvider).Returns(streamProviderMock.Object);

        return providerMock.Object;
    }

    private static async IAsyncEnumerable<VideoStreamsForEpisode> Default(string url, int total, System.Range range)
    {
        await Task.Delay(0);
        var ep = range.End.Value;
        yield return new VideoStreamsForEpisode
        {
            Episode = ep,
            Qualities = new Dictionary<string, VideoStream>
            {
                ["default"] = new VideoStream
                {
                    Quality = "default",
                    Url = $"{url}_stream_{ep}"
                }
            }
        };
    }

    private static async IAsyncEnumerable<SearchResult> RepeatResults(SearchResult result, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new SearchResult
            {
                Title = $"{result.Title}_{i}",
                Url = $"{result.Url}_{i}"
            };
            await Task.Delay(0);
        }
    }
}
