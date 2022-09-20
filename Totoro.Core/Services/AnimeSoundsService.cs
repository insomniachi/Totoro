using System.Text.Json.Nodes;

namespace Totoro.Core.Services;

public class AnimeSoundsService : IAnimeSoundsService
{
    private readonly HttpClient _httpClient;

    public AnimeSoundsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public IList<AnimeSound> GetThemes(long id)
    {
        try
        {
            var result = _httpClient.GetStringAsync($"https://themes.moe/api/themes/{id}").Result;
            var jObject = JsonNode.Parse(result).AsArray();

            if (jObject is null)
            {
                return Array.Empty<AnimeSound>();
            }

            if (jObject[0]["themes"].AsArray() is not JsonArray array)
            {
                return Array.Empty<AnimeSound>();
            }

            var themes = new List<AnimeSound>();

            foreach (var item in array)
            {
                themes.Add(new AnimeSound
                {
                    Name = (string)item["themeType"].AsValue(),
                    SongName = (string)item["themeName"].AsValue(),
                    Url = (string)item["mirror"]["mirrorURL"].AsValue()
                });
            }

            return themes;
        }
        catch
        {
            return Array.Empty<AnimeSound>();
        }
    }
}
