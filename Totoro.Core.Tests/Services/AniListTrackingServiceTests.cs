using System.Reactive.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Totoro.Core.Services.AniList;

namespace Totoro.Core.Tests.Services;

public class AniListTrackingServiceTests
{
    private readonly string _settings = @"C:\Users\athul\AppData\Local\Totoro\ApplicationData\LocalSettings.json";
    private readonly AniListAuthToken _token;

    public AniListTrackingServiceTests()
    {
        var settingsObj = JsonNode.Parse(File.ReadAllText(_settings));
        var anilistTokenString = (string)settingsObj["AniListToken"].AsValue();
        _token = JsonSerializer.Deserialize<AniListAuthToken>(anilistTokenString);
    }

    [Fact]
    public async Task GetAnime()
    {
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock.Setup(x => x.ReadSetting("AniListToken", It.IsAny<AniListAuthToken>())).Returns(_token);

        var service = new AniListTrackingService(localSettingsServiceMock.Object);
        var result = await service.GetAnime().ToListAsync();
    }

    [Fact]
    public async Task Update()
    {
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock.Setup(x => x.ReadSetting("AniListToken", It.IsAny<AniListAuthToken>())).Returns(_token);

        var service = new AniListTrackingService(localSettingsServiceMock.Object);
        var result = await service.Update(339, new Tracking
        {
            WatchedEpisodes = 13,
            FinishDate = DateTime.Now,
            Status = AnimeStatus.Completed
        });
    }

    [Fact]
    public async Task GetSeasonal()
    {
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock.Setup(x => x.ReadSetting("AniListToken", It.IsAny<AniListAuthToken>())).Returns(_token);

        var service = new AnilistService(localSettingsServiceMock.Object, Mock.Of<IAnimeIdService>(), Mock.Of<ISettings>());

        await service.GetSeasonalAnime().ForEachAsync(x =>
        {
            var items = x;
        });
    }

    [Fact]
    public async Task GetById()
    {
        var localSettingsServiceMock = new Mock<ILocalSettingsService>();
        localSettingsServiceMock.Setup(x => x.ReadSetting("AniListToken", It.IsAny<AniListAuthToken>())).Returns(_token);

        var service = new AnilistService(localSettingsServiceMock.Object, Mock.Of<IAnimeIdService>(), Mock.Of<ISettings>());

        var result = await service.GetInformation(31646);
    }


}
