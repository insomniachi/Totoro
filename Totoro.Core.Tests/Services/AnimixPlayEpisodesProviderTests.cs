using System.Reactive.Linq;
using Totoro.Core.Services.AnimixPlay;
using System.Reactive.Threading.Tasks;

namespace Totoro.Core.Tests.Services;

public class AnimixPlayEpisodesProviderTests
{
    private readonly HttpClient _httpClient = new();

    [Fact]
    public async void GetMalId_ReturnsMalId()
    {
        // arrange
        var airedEp = new AiredEpisode
        {
            Anime = "Spy x Family Season 2",
            EpisodeUrl = "https://animixplay.to/v1/spy-x-family-part-2/ep11"
        };
        var service = new AnimixPlayEpisodesProvider(_httpClient);

        // act
        var id = await service.GetMalId(airedEp);

        // assert
        Assert.Equal(50602, id);
    }

    [Fact]
    public async void GetRecentlyAiredEpisodes_ReturnsValues()
    {
        // arrange
        var service = new AnimixPlayEpisodesProvider(_httpClient);

        // act
        var eps = await service.GetRecentlyAiredEpisodes().ToTask();

        // assert
        Assert.NotEmpty(eps);
        foreach (var item in eps)
        {
            Assert.False(string.IsNullOrEmpty(item.Anime));
            Assert.False(string.IsNullOrEmpty(item.InfoText));
            Assert.False(string.IsNullOrEmpty(item.Image));
            Assert.NotNull(item.TimeOfAiring);
        }
    }
}
