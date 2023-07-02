using Totoro.Plugins.Anime.Models;

namespace Totoro.Plugins.Anime.AllAnime;

internal static class Config
{
    public static string Url { get; set; } = "https://allanime.to/";
    public static StreamType StreamType { get; set; } = StreamType.EnglishSubbed;
    public static string CountryOfOrigin { get; set; } = "JP";
    public static string Api = "https://api.allanime.day/api";
}
