using System.Net.Http;
using System.Text.Json.Nodes;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Totoro.WinUI.Services;

public class AnimeSoundsService : IAnimeSoundsService
{
    private readonly HttpClient _httpClient;
    private readonly MediaPlayer _mediaPlayer = new();

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

    public void PlaySound(AnimeSound sound)
    {
        _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(sound.Url));
        _mediaPlayer.Play();
    }

    public void Pause()
    {
        _mediaPlayer.Pause();
    }

}
