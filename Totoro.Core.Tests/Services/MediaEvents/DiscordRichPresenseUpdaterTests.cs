using System.Reactive;
using Totoro.Core.Services.MediaEvents;
using Totoro.Core.Tests.Helpers;

namespace Totoro.Core.Tests.Services.MediaEvents;

public class DiscordRichPresenseUpdaterTests
{
    [Fact]
    public async Task UpdatesDuration()
    {
        // arrange
        var drpc = new Mock<IDiscordRichPresense>();
        var settings = new Mock<ISettings>();
        settings.Setup(x => x.UseDiscordRichPresense).Returns(true);
        var service = new DiscordRichPresenseUpdater(drpc.Object, settings.Object);
        var mediaPlayer = new MockMediaPlayer();
        service.SetMediaPlayer(mediaPlayer);

        // act
        await mediaPlayer.SetDuration(TimeSpan.FromMinutes(24));

        // assert
        drpc.Verify(x => x.UpdateTimer(TimeSpan.FromMinutes(24)));
    }

    [Fact]
    public async Task UpdatesAllDetails()
    {
        // arrange
        var drpc = new Mock<IDiscordRichPresense>();
        var settings = new Mock<ISettings>();
        settings.Setup(x => x.UseDiscordRichPresense).Returns(true);
        var service = new DiscordRichPresenseUpdater(drpc.Object, settings.Object);
        var mediaPlayer = new MockMediaPlayer();
        var anime = new AnimeModel
        {
            Title = "Anime",
            Image = "CoverImage"
        };
        service.SetMediaPlayer(mediaPlayer);
        service.SetAnime(anime);
        service.SetCurrentEpisode(1);

        // act
        await mediaPlayer.SetDuration(TimeSpan.FromMinutes(24));
        mediaPlayer.Play();

        // assert
        drpc.Verify(x => x.UpdateDetails(anime.Title), Times.Once);
        drpc.Verify(x => x.UpdateImage(anime.Image), Times.Once);
        drpc.Verify(x => x.UpdateState("Episode 1"), Times.Once);
        drpc.Verify(x => x.UpdateTimer(TimeSpan.FromMinutes(24)), Times.Exactly(2));
    }

    [Fact]
    public void UpdatesPresenceWhenPaused()
    {
        // arrange
        var drpc = new Mock<IDiscordRichPresense>();
        var settings = new Mock<ISettings>();
        settings.Setup(x => x.UseDiscordRichPresense).Returns(true);
        var service = new DiscordRichPresenseUpdater(drpc.Object, settings.Object);
        var mediaPlayer = new MockMediaPlayer();
        var anime = new AnimeModel
        {
            Title = "Anime",
            Image = "CoverImage"
        };
        service.SetMediaPlayer(mediaPlayer);
        service.SetCurrentEpisode(1);
        mediaPlayer.Pause();

        // assert
        drpc.Verify(x => x.ClearTimer(), Times.Once);
        drpc.Verify(x => x.UpdateState("Episode 1 (Paused)"));
    }

    [Fact]
    public void ClearsPresenseWhenFinished()
    {
        // arrange
        var drpc = new Mock<IDiscordRichPresense>();
        var settings = new Mock<ISettings>();
        settings.Setup(x => x.UseDiscordRichPresense).Returns(true);
        var service = new DiscordRichPresenseUpdater(drpc.Object, settings.Object);
        var mediaPlayer = new MockMediaPlayer();
        var anime = new AnimeModel
        {
            Title = "Anime",
            Image = "CoverImage"
        };
        service.SetMediaPlayer(mediaPlayer);
        service.SetCurrentEpisode(1);
        mediaPlayer.PlaybackEndedSubject.OnNext(Unit.Default);

        // assert
        drpc.Verify(x => x.Clear(), Times.Once);
    }
}
