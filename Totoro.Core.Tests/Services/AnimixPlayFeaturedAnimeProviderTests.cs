using System.Reactive.Linq;
using Totoro.Core.Services.AnimixPlay;

namespace Totoro.Core.Tests.Services;

public class AnimixPlayFeaturedAnimeProviderTests
{
    private readonly HttpClient _httpClient = new();

    [Fact]
    public async void GetFeaturedAnime_ReturnsValues()
    {
        // arrage
        var service = new AnimixPlayFeaturedAnimeProvider(_httpClient);

        // act
        var result = await service.GetFeaturedAnime();

        // assert
        Assert.NotEmpty(result);
        foreach (var item in result)
        {
            Assert.False(string.IsNullOrEmpty(item.Genres));
            Assert.False(string.IsNullOrEmpty(item.Id));
            Assert.False(string.IsNullOrEmpty(item.Title));
            Assert.False(string.IsNullOrEmpty(item.Url));
            Assert.False(string.IsNullOrEmpty(item.Description));
            Assert.False(string.IsNullOrEmpty(item.Image));
        }
    }
}
