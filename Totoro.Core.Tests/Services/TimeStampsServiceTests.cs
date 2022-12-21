using Totoro.Core.Services;
using Totoro.Core.Tests.Helpers;

namespace Totoro.Core.Tests.Services;

public class TimeStampsServiceTests
{
    [Fact]
    public async void GetTimeStamps_ReturnsValues_IfFound()
    {
        // arrange
        const long malId = 40748;
        const long aniListId = 113415;
        var fileServiceMock = new Mock<IFileService>();
        fileServiceMock.Setup(x => x.Read<Dictionary<long, List<OfflineEpisodeTimeStamp>>>("", "timestamps_generated.json"))
                       .Returns(new Dictionary<long, List<OfflineEpisodeTimeStamp>>());

        var service = new TimestampsService(GetAnimeIdService(malId, aniListId), fileServiceMock.Object, Mock.Of<ISettings>());
        var expected = SnapshotService.GetSnapshot<AnimeTimeStamps>("JujtsuKaizenTimeStamps");

        // act
        var actual = await service.GetTimeStamps(malId);

        Assert.Equal(expected.EpisodeTimeStamps.Count, actual.EpisodeTimeStamps.Count);

        for (int i = 1; i <= 24; i++)
        {
            var expectedStart = expected.GetIntroStartPosition(i.ToString());
            var expectedEnd = expected.GetOutroStartPosition(i.ToString());
            var actualStart = actual.GetIntroStartPosition(i.ToString());
            var actualEnd = actual.GetOutroStartPosition(i.ToString());

            Assert.Equal(expectedStart, actualStart);
            Assert.Equal(expectedEnd, actualEnd);
        }

    }

    [Fact]
    public async void GetTimeStamps_ReturnsEmpty_IfNotFound()
    {
        // arrange
        const long malId = 12189;
        const long aniListId = 12189;
        var fileServiceMock = new Mock<IFileService>();
        fileServiceMock.Setup(x => x.Read<Dictionary<long, List<OfflineEpisodeTimeStamp>>>("", "timestamps_generated.json"))
                       .Returns(new Dictionary<long, List<OfflineEpisodeTimeStamp>>());

        var service = new TimestampsService(GetAnimeIdService(malId, aniListId), fileServiceMock.Object, Mock.Of<ISettings>());

        // act
        var actual = await service.GetTimeStamps(malId);

        Assert.Empty(actual.EpisodeTimeStamps);
    }

    [Fact]
    public async void GetTimeStamps_ReturnsFromLocal_IfFound()
    {
        // arrange
        const long malId = 12189;
        const long aniListId = 12189;
        const long aniDbId = 8855;
        var fileServiceMock = new Mock<IFileService>();
        fileServiceMock.Setup(x => x.Read<Dictionary<long, List<OfflineEpisodeTimeStamp>>>("", "timestamps_generated.json"))
                       .Returns(new Dictionary<long, List<OfflineEpisodeTimeStamp>>()
                       {
                           [aniDbId] = new List<OfflineEpisodeTimeStamp>
                           {
                               new(){Episode = 1, Intro = 69, Outro = 69, Source = "Mock"}
                           }
                       });

        var service = new TimestampsService(GetAnimeIdService(malId, aniListId, aniDbId), fileServiceMock.Object, Mock.Of<ISettings>());

        // act
        var actual = await service.GetTimeStamps(malId);

        // assert
        var ts = actual.EpisodeTimeStamps["1"];
        Assert.Single(actual.EpisodeTimeStamps);
        Assert.Equal(69, ts.Intro);
        Assert.Equal(69, ts.Outro);
    }

    private static IAnimeIdService GetAnimeIdService(long malId, long aniListId, long aniDbId = 0)
    {
        var animeIdServiceMock = new Mock<IAnimeIdService>();
        animeIdServiceMock.Setup(x => x.GetId(AnimeTrackerType.MyAnimeList, malId))
                          .Returns(Task.FromResult(new AnimeId
                          {
                              MyAnimeList = malId,
                              AniList = aniListId,
                              AniDb = aniDbId
                          }));

        return animeIdServiceMock.Object;
    }
}
