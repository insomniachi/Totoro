using Totoro.Core.Services.MediaEvents;
using Totoro.Core.Tests.Helpers;

namespace Totoro.Core.Tests.Services.MediaEvents;

public class AniskipTests
{
    [Fact]
    public async Task ShowSkipButtonDuringOp()
    {
        // arrange
        var viewService = new Mock<IViewService>();
        var settings = new Mock<ISettings>();
        var service = new Aniskip(viewService.Object, settings.Object);
        var skipTime = new TimestampResult
        {
            Items = new[]
            {
                new Timestamp()
                {
                    SkipType = "op",
                    Interval = new Interval
                    {
                        StartTime = 30,
                        EndTime = 120
                    }
                },
                new Timestamp()
                {
                    SkipType = "ed",
                    Interval = new Interval
                    {
                        StartTime = 1300,
                        EndTime = 1420
                    }
                }
            }
        };
        var mediaPlayer = new MockMediaPlayer();
        service.SetMediaPlayer(mediaPlayer);
        service.SetCurrentEpisode(1);
        service.SetTimeStamps(skipTime);

        // act and verify
        await mediaPlayer.SetPosition(TimeSpan.FromSeconds(skipTime.Opening.Interval.StartTime));
        mediaPlayer.TransportControlsMock.VerifySet(x => x.IsSkipButtonVisible = true);
        await mediaPlayer.SetPosition(TimeSpan.FromSeconds(skipTime.Opening.Interval.EndTime + 1));
        mediaPlayer.TransportControlsMock.VerifySet(x => x.IsSkipButtonVisible = false);
        await mediaPlayer.SetPosition(TimeSpan.FromSeconds(skipTime.Ending.Interval.StartTime));
        mediaPlayer.TransportControlsMock.VerifySet(x => x.IsSkipButtonVisible = true);
        await mediaPlayer.SetPosition(TimeSpan.FromSeconds(skipTime.Ending.Interval.EndTime + 1));
        mediaPlayer.TransportControlsMock.VerifySet(x => x.IsSkipButtonVisible = false);
    }

    [Fact]
    public async Task PressingSkipButtonSkipsOpening()
    {
        // arrange
        var viewService = new Mock<IViewService>();
        var settings = new Mock<ISettings>();
        var service = new Aniskip(viewService.Object, settings.Object);
        var skipTime = new TimestampResult
        {
            Items = new[]
            {
                new Timestamp()
                {
                    SkipType = "op",
                    Interval = new Interval
                    {
                        StartTime = 30,
                        EndTime = 120
                    }
                }
            }
        };
        var mediaPlayer = new MockMediaPlayer();
        service.SetMediaPlayer(mediaPlayer);
        service.SetCurrentEpisode(1);
        service.SetTimeStamps(skipTime);

        // act
        await mediaPlayer.SetPosition(TimeSpan.FromSeconds(skipTime.Opening.Interval.StartTime));
        mediaPlayer.PressDynamicSkip();

        // verify
        Assert.Equal(skipTime.Opening.Interval.EndTime, mediaPlayer.LastSeekedTime.TotalSeconds);
    }

    [Fact]
    public async Task PressingSkipButtonSkipsEnding()
    {
        // arrange
        var viewService = new Mock<IViewService>();
        var settings = new Mock<ISettings>();
        var service = new Aniskip(viewService.Object, settings.Object);
        var skipTime = new TimestampResult
        {
            Items = new[]
            {
                new Timestamp()
                {
                    SkipType = "ed",
                    Interval = new Interval
                    {
                        StartTime = 1300,
                        EndTime = 1420
                    }
                }
            }
        };
        var mediaPlayer = new MockMediaPlayer();
        service.SetMediaPlayer(mediaPlayer);
        service.SetCurrentEpisode(1);
        service.SetTimeStamps(skipTime);

        // act
        await mediaPlayer.SetPosition(TimeSpan.FromSeconds(skipTime.Ending.Interval.StartTime));
        mediaPlayer.PressDynamicSkip();

        // verify
        Assert.Equal(skipTime.Ending.Interval.EndTime, mediaPlayer.LastSeekedTime.TotalSeconds);
    }
}
