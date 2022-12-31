using System.Reactive;
using System.Reactive.Linq;
using Totoro.Core.Tests.Builders;
using Totoro.Core.Tests.Helpers;

namespace Totoro.Core.Tests.ViewModels;

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
        vmBuilder.MediaPlayer.PlayingSubject.OnNext(Unit.Default);
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
                x.Setup(x => x.Update(animeModel.Id, It.IsAny<Tracking>())).Returns(Observable.Return(newTracking));
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
                x.Setup(x => x.Update(animeModel.Id, It.IsAny<Tracking>())).Returns(Observable.Return(newTracking));
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
                x.Setup(x => x.Update(animeModel.Id, It.IsAny<Tracking>())).Returns(Observable.Return(newTracking));
            });

        var vm = vmBuilder.Bulid();
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // act
        vmBuilder.MediaPlayer.PlaybackEndedSubject.OnNext(Unit.Default);

        // assert
        Assert.Equal(12, vm.CurrentEpisode);
    }

    [Fact]
    public void WatchViewModel_PopulatesEpisodes()
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
        var vmBuilder = BaseViewModel(provider);
        var vm = vmBuilder.Bulid();
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // assert
        Assert.Equal(24, vm.Episodes.Count);
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

        vm.OnNavigatedTo(new Dictionary<string, object> { ["Anime"] = animeModel }).Wait();

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
            x.Verify(x => x.UpdateState($"Episode {animeModel.Tracking.WatchedEpisodes + 1} (Paused)"), Times.Once);
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

    [Fact]
    public void WatchViewModel_HasSearchBoxWhenNavigatedWithoutAnime()
    {
        // arrange
        var vm = BaseViewModel(Mock.Of<IProvider>()).Bulid();

        // act
        vm.OnNavigatedTo(new Dictionary<string, object>()).Wait();

        Assert.False(vm.HideControls);
    }

    [Fact]
    public void WatchViewModel_HidesSearchBoxWhenNavigatedWithAnime()
    {
        // arrange
        AnimeModel animeModel = new()
        {
            Id = 12189,
            Title = "Hyouka",
            TotalEpisodes = 24,
            Tracking = new Tracking
            {
                WatchedEpisodes = 10
            }
        };
        var provider = GetProvider(new SearchResult { Title = "Hyouka" }, 24);
        var vm1 = BaseViewModel(provider).Bulid();
        var vm2 = BaseViewModel(provider)
            .WithAnimeService(x =>
            {
                x.Setup(x => x.GetInformation(animeModel.Id)).Returns(Observable.Return(animeModel));
            })
            .Bulid();
        var vm3 = BaseViewModel(provider)
            .WithAnimeService(x =>
            {
                x.Setup(x => x.GetInformation(animeModel.Id)).Returns(Observable.Return(animeModel));
            })
            .Bulid();

        // act
        vm1.OnNavigatedTo(new Dictionary<string, object> { ["Anime"] = animeModel }).Wait();
        vm2.OnNavigatedTo(new Dictionary<string, object>
        {
            ["EpisodeInfo"] = new TestAiredEpisode
            {
                Title = animeModel.Title,
                Url = "https://animixplay.to/v1/hyouka",
            }
        }).Wait();
        vm3.OnNavigatedTo(new Dictionary<string, object> { ["Id"] = animeModel.Id }).Wait();

        // assert
        Assert.True(vm1.HideControls);
        Assert.True(vm2.HideControls);
        Assert.True(vm3.HideControls);
    }

    [Fact]
    public void WatchViewModel_ClickingNext_AfterTrackingAlreadyUpdated()
    {
        // arrange
        AnimeModel animeModel = new()
        {
            Id = 12189,
            Title = "Hyouka",
            TotalEpisodes = 24,
            Tracking = new Tracking
            {
                WatchedEpisodes = 10
            }
        };
        var provider = GetProvider(new SearchResult { Title = "Hyouka" }, 24);
        var vmBuilder = BaseViewModel(provider);
        var vm = vmBuilder.Bulid();

        // act
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // started playing
        vmBuilder.MediaPlayer.DurationChangedSubject.OnNext(TimeSpan.FromMinutes(24));
        vmBuilder.MediaPlayer.PlayingSubject.OnNext(Unit.Default);
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.Zero);

        Assert.Equal(11, vm.CurrentEpisode);

        // reached time to update tracking
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.FromMinutes(23)); // less than 2 min remaining

        vmBuilder.VerifyTrackingService(tracking =>
        {
            tracking.Verify(x => x.Update(animeModel.Id, new Tracking { WatchedEpisodes = 11 }), Times.Once);
        });

        Assert.Equal(11, vm.Anime.Tracking.WatchedEpisodes);

        // click next button
        vm.NextEpisode.Execute(null);

        Assert.Equal(12, vm.CurrentEpisode);

        // don't update tracking again
        vmBuilder.VerifyTrackingService(tracking =>
        {
            tracking.Verify(x => x.Update(animeModel.Id, new Tracking { WatchedEpisodes = 12 }), Times.Never);
        });
    }

    [Fact]
    public async void WatchViewModel_MarksAsCompleteWhenFinishingLastEpisode()
    {
        // arrange
        AnimeModel animeModel = new()
        {
            Id = 12189,
            Title = "Hyouka",
            TotalEpisodes = 24,
            Tracking = new Tracking
            {
                WatchedEpisodes = 23
            }
        };
        var provider = GetProvider(new SearchResult { Title = "Hyouka" }, 24);
        var vmBuilder = BaseViewModel(provider);
        var vm = vmBuilder.Bulid();

        // act
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // started playing
        vmBuilder.MediaPlayer.DurationChangedSubject.OnNext(TimeSpan.FromMinutes(24));
        vmBuilder.MediaPlayer.PlayingSubject.OnNext(Unit.Default);
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.Zero);

        // reached time to update tracking
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.FromMinutes(23));

        await Task.Delay(10);

        Assert.Equal(24, vm.Anime.Tracking.WatchedEpisodes);
        Assert.Equal(AnimeStatus.Completed, vm.Anime.Tracking.Status);
        Assert.Equal(DateTime.Today, vm.Anime.Tracking.FinishDate.Value.Date);
    }

    [Fact]
    public async void WatchViewModel_AddsTrackingWheningFinishingFirstEpisodeOfUntracked()
    {
        // arrange
        AnimeModel animeModel = new()
        {
            Id = 12189,
            Title = "Hyouka",
            TotalEpisodes = 24,
        };
        var provider = GetProvider(new SearchResult { Title = "Hyouka" }, 24);
        var vmBuilder = BaseViewModel(provider);
        var vm = vmBuilder.Bulid();

        // act
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // started playing
        vmBuilder.MediaPlayer.DurationChangedSubject.OnNext(TimeSpan.FromMinutes(24));
        vmBuilder.MediaPlayer.PlayingSubject.OnNext(Unit.Default);
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.Zero);

        // reached time to update tracking
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.FromMinutes(23));

        await Task.Delay(10);

        Assert.Equal(1, vm.Anime.Tracking.WatchedEpisodes);
        Assert.Equal(AnimeStatus.Watching, vm.Anime.Tracking.Status);
        Assert.Equal(DateTime.Today, vm.Anime.Tracking.StartDate.Value.Date);
    }

    [Fact]
    public async void WatchViewModel_WillNotUpdateTrackingIfRewatchingPrevEpisode()
    {
        // arrange
        AnimeModel animeModel = new()
        {
            Id = 12189,
            Title = "Hyouka",
            TotalEpisodes = 24,
            Tracking = new Tracking
            {
                WatchedEpisodes = 23
            }
        };
        var provider = GetProvider(new SearchResult { Title = "Hyouka" }, 24);
        var vmBuilder = BaseViewModel(provider);
        var vm = vmBuilder.Bulid();

        // act
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();
        vm.CurrentEpisode = 12;
        await Task.Delay(10);

        vmBuilder.MediaPlayer.DurationChangedSubject.OnNext(TimeSpan.FromMinutes(24));
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.FromMinutes(23));

        // api request was never made.
        vmBuilder.VerifyTrackingService(tracking =>
        {
            tracking.Verify(x => x.Update(animeModel.Id, It.IsAny<Tracking>()), Times.Never);
        });
    }

    [Fact]
    public async void WatchViewModel_TrackingWillNotUpdateMultipleTimesAfterEnd()
    {
        // arrange
        AnimeModel animeModel = new()
        {
            Id = 12189,
            Title = "Hyouka",
            TotalEpisodes = 24,
            Tracking = new Tracking
            {
                WatchedEpisodes = 10
            }
        };
        var provider = GetProvider(new SearchResult { Title = "Hyouka" }, 24);
        var vmBuilder = BaseViewModel(provider);
        var vm = vmBuilder.Bulid();

        // act
        vm.OnNavigatedTo(new Dictionary<string, object> { [nameof(vm.Anime)] = animeModel }).Wait();

        // started playing
        vmBuilder.MediaPlayer.DurationChangedSubject.OnNext(TimeSpan.FromMinutes(24));
        vmBuilder.MediaPlayer.PlayingSubject.OnNext(Unit.Default);
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.Zero);

        // reached time to update tracking and also clicked next button
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.FromMinutes(23));
        vmBuilder.MediaPlayer.PositionChangedSubject.OnNext(TimeSpan.FromMinutes(23) + TimeSpan.FromSeconds(15));
        vm.NextEpisode.Execute(null);

        await Task.Delay(10);

        vmBuilder.VerifyTrackingService(tracking =>
        {
            tracking.Verify(x => x.Update(animeModel.Id, It.IsAny<Tracking>()), Times.Once);
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
                settings.Setup(x => x.TimeRemainingWhenEpisodeCompletesInSeconds).Returns(120);
            })
            .WithTrackingService(tracking =>
            {
                tracking.Setup(x => x.Update(It.IsAny<long>(), It.IsAny<Tracking>()))
                        .Returns<long, Tracking>((_, tracking) => Observable.Return(tracking));
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
