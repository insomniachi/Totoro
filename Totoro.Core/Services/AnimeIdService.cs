using System.Diagnostics;

namespace Totoro.Core.Services;

public class AnimeIdService : IAnimeIdService
{
    private readonly HttpClient _httpClient;

    public AnimeIdService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AnimeId> GetId(ListServiceType serviceType, long id)
    {
        var source = serviceType switch
        {
            ListServiceType.AniDb => "anidb",
            ListServiceType.AniList => "anilist",
            ListServiceType.MyAnimeList => "myanimelist",
            ListServiceType.Kitsu => "kitsu",
            _ => throw new UnreachableException()
        };

        var stream = await _httpClient.GetStreamAsync($"https://arm.haglund.dev/api/ids?source={source}&id={id}");
        return await JsonSerializer.DeserializeAsync(stream, AnimeIdSerializerContext.Default.AnimeId);
    }
}
