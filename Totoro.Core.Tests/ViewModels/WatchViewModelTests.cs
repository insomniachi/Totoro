using System.Reactive;
using System.Reactive.Linq;
using AnimDL.Api;
using Totoro.Core.Tests.Builders;
using Totoro.Tests.Helpers;

namespace Totoro.WinUI.Tests.ViewModels;

public class WatchViewModelTests
{
    [Theory]
    [InlineData(24, 0)]
    [InlineData(24, 10)]
    [InlineData(24, 23)]
    public void WatchViewModel_AfterNavigation_PlaysFirstUnwatchedEpisode(int totalEpisodes, int lastEpisodeCompleted)
    {
        // arrange
        IAnimeModel animeModel = new AnimeModel()
        {
            TotalEpisodes = totalEpisodes,
            Tracking = new Tracking
            {
                WatchedEpisodes = lastEpisodeCompleted
            }
        };
        
        var result = new SearchResult()
        {
            Title = "Hyouka",
            Url = "https://animixplay.to/v1/hyouka"
        };

        var vm = BaseViewModel(GetProvider(result, totalEpisodes)).Bulid();

        // act
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // assert
        Assert.Equal(lastEpisodeCompleted + 1, vm.CurrentEpisode);
    }

    [Fact]
    public void WatchViewModel_AfterNavigation_PlaysNothingIfNoNewEpisodes()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult()
        {
            Title = "Hyouka",
            Url = "https://animixplay.to/v1/hyouka"
        };
        IAnimeModel animeModel = new AnimeModel()
        {
            Title = "Hyouka",
            TotalEpisodes = ep,
            Tracking = new Tracking
            {
                WatchedEpisodes = ep
            }
        };
        var provider = GetProvider(result, ep);
        var vm = BaseViewModel(provider).Bulid();

        // act
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // assert
        Assert.Null(vm.CurrentEpisode);
    }

    [Fact]
    public void WatchViewModel_AfterNavigation_UntrackedAnimeStartsFromFirstEpisode()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult()
        {
            Title = "Hyouka",
            Url = "https://animixplay.to/v1/hyouka"
        };
        IAnimeModel animeModel = new AnimeModel()
        {
            Title = "Hyouka",
            TotalEpisodes = ep,
        };
        var provider = GetProvider(result, ep);
        var vm = BaseViewModel(provider).Bulid();

        // act
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // assert
        Assert.Equal(1, vm.CurrentEpisode);
    }

    [Fact]
    public void WatchViewModel_OnUseDubChanged_ChangesStream()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult()
        {
            Title = "Hyouka",
            Url = "https://animixplay.to/v1/hyouka"
        };
        IAnimeModel animeModel = new AnimeModel()
        {
            Title = "Hyouka",
            TotalEpisodes = ep,
        };
        var provider = GetProvider(result, ep);
        var vm = BaseViewModel(provider).Bulid();
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();
        
        // act
        vm.UseDub = true;
        
        // assert
        Assert.Equal("Hyouka Dub", vm.SelectedAudio.Title);

        // act
        vm.UseDub = false;
        
        // assert
        Assert.Equal("Hyouka", vm.SelectedAudio.Title);
    }

    [Theory]
    [InlineData(5.0)]
    [InlineData(15.0)]
    public void WatchViewModel_SavesPositionWhenWatchedOver10Seconds(double time)
    {
        // arrange
        var ep = 24;
        var result = new SearchResult()
        {
            Title = "Hyouka",
            Url = "https://animixplay.to/v1/hyouka"
        };
        var provider = GetProvider(result, ep);

        var vmBuilder = BaseViewModel(provider)
            .WithSettings(x =>
            {
                x.Setup(x => x.UseDiscordRichPresense).Returns(true);
                x.Setup(x => x.PreferSubs).Returns(true);
            });
        var vm = vmBuilder.Bulid();
        IAnimeModel animeModel = new AnimeModel()
        {
            Id = 12189,
            Title = "Hyouka",
            TotalEpisodes = ep,
            Tracking = new Tracking
            {
                WatchedEpisodes = 10
            }
        };

        vm.OnNavigatedTo(new Dictionary<string, object> { ["Anime"] = animeModel }).Wait();

        // act
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.FromSeconds(time));

        var count = time > 10 ? Times.Once() : Times.Never();
        vmBuilder.VerifyPlaybackStateStorage(x =>
        {
            x.Verify(x => x.Update(animeModel.Id, vm.CurrentEpisode.Value, time), count);
        });
    }

    [Fact]
    public void WatchViewModel_UpdatesStatusWhenMediaFinishes()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult()
        {
            Title = "Hyouka",
            Url = "https://animixplay.to/v1/hyouka"
        };
        IAnimeModel animeModel = new AnimeModel()
        {
            Title = "Hyouka",
            TotalEpisodes = ep,
            Tracking = new Tracking
            {
                WatchedEpisodes = 10
            }
        };
        var provider = GetProvider(result, ep);
        var newTracking = new Tracking { WatchedEpisodes = 11 };
        var vmBuilder = BaseViewModel(provider)
            .WithTrackingService(x =>
            {
                x.Setup(x => x.Update(animeModel.Id, It.IsAny<Tracking>())).Returns(Observable.Start(() => newTracking));
            });

        var vm = vmBuilder.Bulid();
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // act
        vmBuilder.MediaPlayer.PlaybackEndedSubject.OnNext(Unit.Default);

        vmBuilder.VerifyTrackingService(x =>
        {
            x.Verify(x => x.Update(animeModel.Id, newTracking));
        });
    }

    [Fact]
    public void WatchViewModel_UpdatesStatusWhenConfiguredTimeReaches()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult()
        {
            Title = "Hyouka",
            Url = "https://animixplay.to/v1/hyouka"
        };
        IAnimeModel animeModel = new AnimeModel()
        {
            Title = "Hyouka",
            TotalEpisodes = ep,
            Tracking = new Tracking
            {
                WatchedEpisodes = 10
            }
        };
        var provider = GetProvider(result, ep);
        var newTracking = new Tracking { WatchedEpisodes = 11 };
        var vmBuilder = BaseViewModel(provider)
            .WithTrackingService(x =>
            {
                x.Setup(x => x.Update(animeModel.Id, It.IsAny<Tracking>())).Returns(Observable.Start(() => newTracking));
            })
            .WithSettings(settings =>
            {
                settings.Setup(x => x.TimeRemainingWhenEpisodeCompletesInSeconds).Returns(90);
            });

        var vm = vmBuilder.Bulid();
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // act
        vmBuilder.MediaPlayer.DurationChangedSubject.OnNext(TimeSpan.FromMinutes(24));
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.FromMinutes(24) - TimeSpan.FromSeconds(90));

        vmBuilder.VerifyTrackingService(x =>
        {
            x.Verify(x => x.Update(animeModel.Id, newTracking));
        });
    }

    [Fact]
    public void WatchViewModel_PlaysNextEpisodeWhenCurrentFinishes()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult()
        {
            Title = "Hyouka",
            Url = "https://animixplay.to/v1/hyouka"
        };
        IAnimeModel animeModel = new AnimeModel()
        {
            Title = "Hyouka",
            TotalEpisodes = ep,
            Tracking = new Tracking
            {
                WatchedEpisodes = 10
            }
        };
        var provider = GetProvider(result, ep);
        var newTracking = new Tracking { WatchedEpisodes = 11 };
        var vmBuilder = BaseViewModel(provider)
            .WithTrackingService(x =>
            {
                x.Setup(x => x.Update(animeModel.Id, It.IsAny<Tracking>())).Returns(Observable.Start(() => newTracking));
            });

        var vm = vmBuilder.Bulid();
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // act
        vmBuilder.MediaPlayer.PlaybackEndedSubject.OnNext(Unit.Default);

        // assert
        Assert.Equal(12, vm.CurrentEpisode);
    }



    [Fact]
    public void WatchViewModel_DiscordRpcWorks()
    {
        // arrange
        var ep = 24;
        var result = new SearchResult()
        {
            Title = "Hyouka",
            Url = "https://animixplay.to/v1/hyouka"
        };
        var provider = GetProvider(result, ep);

        var vmBuilder = BaseViewModel(provider)
            .WithDiscordRpc(x => { })
            .WithSettings(x =>
            {
                x.Setup(x => x.UseDiscordRichPresense).Returns(true);
                x.Setup(x => x.PreferSubs).Returns(true);
            });
        var vm = vmBuilder.Bulid();
        IAnimeModel animeModel = new AnimeModel()
        {
            Title = "Hyouka",
            TotalEpisodes = ep,
            Tracking = new Tracking
            {
                WatchedEpisodes = 10
            }
        };

        vm.OnNavigatedTo(new Dictionary<string, object>{["Anime"] = animeModel }).Wait();
        
        // act
        vm.ChangeQuality.Execute("default");
        vmBuilder.MediaPlayer.DurationChangedSubject.OnNext(TimeSpan.FromMinutes(24));
        vmBuilder.MediaPlayer.PlayingSubject.OnNext(Unit.Default);
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.Zero);

        // Assert
        vmBuilder.VerifyDiscordRpc(x =>
        {
            x.Verify(x => x.UpdateDetails(animeModel.Title));
            x.Verify(x => x.UpdateState($"Episode {animeModel.Tracking.WatchedEpisodes + 1}"));
            x.Verify(x => x.UpdateTimer(TimeSpan.FromMinutes(24)));
        });

        // act
        vmBuilder.MediaPlayer.PausedSubject.OnNext(Unit.Default);
        
        // assert
        vmBuilder.VerifyDiscordRpc(x =>
        {
            x.Verify(x => x.UpdateDetails("Paused"), Times.Once);
            x.Verify(x => x.ClearTimer(), Times.Once);
        });

        // act
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.FromMinutes(1));
        vmBuilder.MediaPlayer.PlayingSubject.OnNext(Unit.Default);

        // assert
        vmBuilder.VerifyDiscordRpc(x =>
        {
            x.Verify(x => x.UpdateDetails(animeModel.Title));
            x.Verify(x => x.UpdateState($"Episode {animeModel.Tracking.WatchedEpisodes + 1}"));
            x.Verify(x => x.UpdateTimer(TimeSpan.FromMinutes(23)));
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
                settings.Setup(x => x.PreferSubs).Returns(true);
                settings.Setup(x => x.ElementTheme).Returns(ElementTheme.Dark);
            });

    private static IProvider GetProvider(SearchResult result, int numberOfEps)
    {
        var catalogMock = new Mock<TestCatalog>();
        catalogMock.Setup(x => x.SearchByMalId(It.IsAny<long>())).Returns(Task.FromResult((result, new SearchResult
        {
            Title = result.Title + " Dub",
            Url = result.Url + "-dub"
        })));
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
