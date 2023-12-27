using Totoro.Plugins.Anime.Models;

namespace Totoro.Plugins.Anime.YugenAnime;

public static class Config
{
    public static string Url { get; set; } = "https://yugenanime.tv/";
    public static StreamType StreamType { get; set; } = StreamType.Subbed(Languages.English);
}
