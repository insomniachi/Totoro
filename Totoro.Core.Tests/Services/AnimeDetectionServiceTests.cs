using MalApi;
using Totoro.Core.Services;
using Totoro.Core.Services.MyAnimeList;

namespace Totoro.Core.Tests.Services;

public class AnimeDetectionServiceTests
{
    class TheoryData : TheoryData<string, long>
    {
        public TheoryData()
        {
            // Misc
            Add("[SubsPlease] Dark Gathering - 09 (1080p) [065676B1].mkv", 52505);

            // Summer 2023
            Add("[Erai-raws] Jujutsu Kaisen 2nd Season - 06 [1080p][HEVC][Multiple Subtitle] [ENG][POR-BR][SPA-LA][SPA][ARA][FRE][GER][ITA][RUS]", 51009);
            Add("[Retr0] Zom 100 - Zombie ni Naru made ni Shitai 100 no Koto | Bucket List of the Dead - S01E07 (WEB 1080p AV1) [Multi-Subs] (Weekly)", 54112);
            Add("[Anime Chap] Tate no Yuusha no Nariagari S03E03 [WEB 1080p] {OP & ED Lyrics} - Episode 3 (The Rising of the Shield Hero)", 40357);
            Add("[EMBER] Horimiya: Piece S01E10 [1080p] [HEVC WEBRip] (Horimiya: The Missing Pieces)", 54856);
            Add("Goblin Slayer II", 47160);
            //Add("[ToonsHub] Mushoku Tensei: Jobless Reincarnation - S02E09 (Japanese 2160p x264 AAC) [Multi-Subs]", 51179);
        }
    }

    private readonly MalClient _client;

    public AnimeDetectionServiceTests()
    {
        _client = new MalClient();
        var settings = new LocalSettingsService(new KnownFolders());
        var token = settings.ReadSetting<OAuthToken>("MalToken");
        _client.SetAccessToken(token.AccessToken);
    }

    [Theory]
    [ClassData(typeof(TheoryData))]
    public async Task DetectsFromFileName_Works(string fileName, long expected)
    {
        // arrange
        var settings = new Mock<ISettings>();
        settings.Setup(x => x.DefaultListService).Returns(ListServiceType.MyAnimeList);
        var malService = new MyAnimeListService(_client, Mock.Of<IAnilistService>(), Mock.Of<IAnimeIdService>(), settings.Object);
        var connectivityService = new Mock<IConnectivityService>();
        connectivityService.Setup(x => x.IsConnected).Returns(true);
        var animeServiceContext = new AnimeServiceContext(settings.Object, 
                                                          new Lazy<IAnimeService>(() => malService),
                                                          new Lazy<IAnimeService>(() => malService),
                                                          new Lazy<IAnimeService>(() => malService),
                                                          connectivityService.Object);

        var sut = new AnimeDetectionService(Mock.Of<IViewService>(),
                                            Mock.Of<IToastService>(),
                                            animeServiceContext);

        // act
        var id = await sut.DetectFromFileName(fileName);

        // assert
        Assert.Equal(expected, id);
    }
}
