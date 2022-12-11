using System.Diagnostics;

namespace Totoro.Core.Services;

public class AnimeIdService : IAnimeIdService
{
    private readonly HttpClient _httpClient;

    public AnimeIdService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AnimeId> GetId(AnimeTrackerType serviceType, long id)
    {
        var source = serviceType switch
        {
            AnimeTrackerType.AniDb => "anidb",
            AnimeTrackerType.AniList => "anilist",
            AnimeTrackerType.MyAnimeList => "myanimelist",
            AnimeTrackerType.Kitsu => "kitsu",
            _ => throw new UnreachableException()
        };

        var stream = await _httpClient.GetStreamAsync($"https://arm.haglund.dev/api/ids?source={source}&id={id}");
        return await JsonSerializer.DeserializeAsync(stream, AnimeIdSerializerContext.Default.AnimeId);
    }
}
