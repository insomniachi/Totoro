using System.Diagnostics;

namespace Totoro.Core.Services;

public class AnimeIdService : IAnimeIdService
{
    private readonly HttpClient _httpClient;
    private readonly ISettings _settings;

    public AnimeIdService(HttpClient httpClient,
                          ISettings settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public Task<AnimeId> GetId(ListServiceType serviceType, long id) => GetIdInternal(ConvertType(serviceType), id);
    public Task<AnimeId> GetId(long id) => GetIdInternal(ConvertType(_settings.DefaultListService), id);
    
    private static string ConvertType(ListServiceType? type)
    {
        return type switch
        {
            ListServiceType.AniDb => "anidb",
            ListServiceType.AniList => "anilist",
            ListServiceType.MyAnimeList => "myanimelist",
            ListServiceType.Kitsu => "kitsu",
            _ => throw new UnreachableException()
        };
    }

    private async Task<AnimeId> GetIdInternal(string source, long id)
    {
        var stream = await _httpClient.GetStreamAsync($"https://arm.haglund.dev/api/ids?source={source}&id={id}");
        return await JsonSerializer.DeserializeAsync(stream, AnimeIdSerializerContext.Default.AnimeId);
    }
}
