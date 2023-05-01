using Totoro.Core.Services.MediaEvents;
using Totoro.Core.Tests.Helpers;

namespace Totoro.Core.Tests.Services.MediaEvents;

public class TrackingUpdaterTests
{
    [Fact]
    public async Task UpdatesTracking_WhenTimeReached()
    {
        // arrange
        var trackingService = new Mock<ITrackingServiceContext>();
        var settings = new Mock<ISettings>();
        settings.Setup(x => x.TimeRemainingWhenEpisodeCompletesInSeconds).Returns(120);
        var viewService = new Mock<IViewService>();
        var mediaPlayer = new MockMediaPlayer();
        var animeModel = new AnimeModel
        {
            Id = 10,
            Tracking = new Tracking
            {
                WatchedEpisodes = 5,
            }
        };
        var service = new TrackingUpdater(trackingService.Object, settings.Object, viewService.Object, Mock.Of<ISystemClock>());
        service.SetMediaPlayer(mediaPlayer);
        service.SetAnime(animeModel);
        service.SetCurrentEpisode(animeModel.Tracking.WatchedEpisodes.Value + 1);

        // act
        await mediaPlayer.SetDuration(TimeSpan.FromMinutes(24));
        await mediaPlayer.SetPosition(TimeSpan.FromMinutes(24) - TimeSpan.FromSeconds(120));
        await mediaPlayer.SetPosition(TimeSpan.FromMinutes(24) - TimeSpan.FromSeconds(100));

        // assert
        var expectedTracking = new Tracking
        {
            WatchedEpisodes = animeModel.Tracking.WatchedEpisodes.Value + 1
        };
        trackingService.Verify(x => x.Update(animeModel.Id, expectedTracking), Times.Once);
    }

    [Fact]
    public async Task UpdatesTracking_WhenWatchingFirstEpisode()
    {        
        // arrange
        var trackingService = new Mock<ITrackingServiceContext>();
        var settings = new Mock<ISettings>();
        settings.Setup(x => x.TimeRemainingWhenEpisodeCompletesInSeconds).Returns(120);
        var viewService = new Mock<IViewService>();
        var mediaPlayer = new MockMediaPlayer();
        var systemClock = new Mock<ISystemClock>();
        var today = DateTime.Today;
        systemClock.Setup(x => x.Today).Returns(today);
        var animeModel = new AnimeModel
        {
            Id = 10,
        };
        var service = new TrackingUpdater(trackingService.Object, settings.Object, viewService.Object, systemClock.Object);
        service.SetMediaPlayer(mediaPlayer);
        service.SetAnime(animeModel);
        service.SetCurrentEpisode(1);

        // act
        await mediaPlayer.SetDuration(TimeSpan.FromMinutes(24));
        await mediaPlayer.SetPosition(TimeSpan.FromMinutes(24) - TimeSpan.FromSeconds(120));

        // assert
        var expectedTracking = new Tracking
        {
            WatchedEpisodes = 1,
            StartDate = today,
            Status = AnimeStatus.Watching
        };
        trackingService.Verify(x => x.Update(animeModel.Id, expectedTracking), Times.Once);

    }

    [Fact]
    public async Task UpdatesTracking_WhenEndingReached()
    {
        // arrange
        var trackingService = new Mock<ITrackingServiceContext>();
        var settings = new Mock<ISettings>();
        settings.Setup(x => x.TimeRemainingWhenEpisodeCompletesInSeconds).Returns(0);
        var viewService = new Mock<IViewService>();
        var mediaPlayer = new MockMediaPlayer();
        var animeModel = new AnimeModel
        {
            Id = 10,
            Tracking = new Tracking
            {
                WatchedEpisodes = 5,
            }
        };
        var skipTime = new AniSkipResult
        {
            Success = true,
            Items = new AniSkipResultItem[]
            {
                new AniSkipResultItem
                {
                    SkipType = "ed",
                    EpisodeLength = 1420,
                    Interval = new Interval
                    {
                        StartTime = 1200,
                        EndTime = 1290
                    }
                }
            }
        };
        var service = new TrackingUpdater(trackingService.Object, settings.Object, viewService.Object, Mock.Of<ISystemClock>());
        service.SetMediaPlayer(mediaPlayer);
        service.SetAnime(animeModel);
        service.SetCurrentEpisode(animeModel.Tracking.WatchedEpisodes.Value + 1);

        // act
        await mediaPlayer.SetDuration(TimeSpan.FromMinutes(24));
        service.SetTimeStamps(skipTime);
        await mediaPlayer.SetPosition(TimeSpan.FromSeconds(skipTime.Ending.Interval.StartTime));

        // assert
        var expectedTracking = new Tracking
        {
            WatchedEpisodes = animeModel.Tracking.WatchedEpisodes.Value + 1
        };
        trackingService.Verify(x => x.Update(animeModel.Id, expectedTracking), Times.Once);
    }

    [Fact]
    public async Task WatchingLastEpisode_CompletesAnime()
    {
        // arrange
        var trackingService = new Mock<ITrackingServiceContext>();
        var settings = new Mock<ISettings>();
        settings.Setup(x => x.TimeRemainingWhenEpisodeCompletesInSeconds).Returns(120);
        var viewService = new Mock<IViewService>();
        var mediaPlayer = new MockMediaPlayer();
        var systemClock = new Mock<ISystemClock>();
        var today = DateTime.Today;
        systemClock.Setup(x => x.Today).Returns(today);
        var animeModel = new AnimeModel
        {
            Id = 10,
            TotalEpisodes = 6,
            Tracking = new Tracking
            {
                WatchedEpisodes = 5,
                Score = 8,
            }
        };
        var service = new TrackingUpdater(trackingService.Object, settings.Object, viewService.Object, systemClock.Object);
        service.SetMediaPlayer(mediaPlayer);
        service.SetAnime(animeModel);
        service.SetCurrentEpisode(animeModel.Tracking.WatchedEpisodes.Value + 1);

        // act
        await mediaPlayer.SetDuration(TimeSpan.FromMinutes(24));
        await mediaPlayer.SetPosition(TimeSpan.FromMinutes(24) - TimeSpan.FromSeconds(120));

        // assert
        var expectedTracking = new Tracking
        {
            WatchedEpisodes = animeModel.Tracking.WatchedEpisodes.Value + 1,
            Status = AnimeStatus.Completed,
            FinishDate = today,
        };

        trackingService.Verify(x => x.Update(animeModel.Id, expectedTracking), Times.Once);
    }

    [Fact]
    public async Task WatchingLastEpisode_OfUnscoredAnimeAsksRatingWhenCompleted()
    {
        // arrange
        var trackingService = new Mock<ITrackingServiceContext>();
        var settings = new Mock<ISettings>();
        settings.Setup(x => x.TimeRemainingWhenEpisodeCompletesInSeconds).Returns(120);
        var viewService = new Mock<IViewService>();
        var mediaPlayer = new MockMediaPlayer();
        var systemClock = new Mock<ISystemClock>();
        var today = DateTime.Today;
        systemClock.Setup(x => x.Today).Returns(today);
        var animeModel = new AnimeModel
        {
            Id = 10,
            TotalEpisodes = 6,
            Tracking = new Tracking
            {
                WatchedEpisodes = 5,
            }
        };
        var service = new TrackingUpdater(trackingService.Object, settings.Object, viewService.Object, systemClock.Object);
        service.SetMediaPlayer(mediaPlayer);
        service.SetAnime(animeModel);
        service.SetCurrentEpisode(animeModel.Tracking.WatchedEpisodes.Value + 1);

        // act
        await mediaPlayer.SetDuration(TimeSpan.FromMinutes(24));
        await mediaPlayer.SetPosition(TimeSpan.FromMinutes(24) - TimeSpan.FromSeconds(120));

        // assert
        viewService.Verify(x => x.RequestRating(animeModel), Times.Once);
    }
}
