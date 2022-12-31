using System.Diagnostics;
using Totoro.Core.Services;

namespace Totoro.Core.Tests.Services;

public class AnimeIdServiceTests
{
    private readonly HttpClient _httpClient = new();

    [Theory]
    [InlineData(ListServiceType.MyAnimeList, (long)12189)]
    [InlineData(ListServiceType.AniDb, (long)8855)]
    [InlineData(ListServiceType.AniList, (long)12189)]
    [InlineData(ListServiceType.Kitsu, (long)6686)]
    [InlineData((ListServiceType)10, (long)0)]
    public async void GetId_ReturnsValue(ListServiceType tracker, long id)
    {
        // arrange
        var expected = new AnimeId
        {
            MyAnimeList = 12189,
            AniDb = 8855,
            AniList = 12189,
            Kitsu = 6686
        };
        var service = new AnimeIdService(_httpClient);

        // act
        try
        {
            var actual = await service.GetId(tracker, id);
            Assert.Equal(expected, actual);
        }
        catch (Exception ex)
        {
            Assert.IsType<UnreachableException>(ex);
        }
    }
}
