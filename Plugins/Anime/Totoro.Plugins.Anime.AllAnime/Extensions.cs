using Totoro.Plugins.Anime.Models;

namespace Totoro.Plugins.Anime.AllAnime;

internal static class Extensions
{
    public static string ConvertToTranslationType(this StreamType streamType)
    {
        return streamType switch
        {
            { AudioLanguage: Languages.Japanese, SubtitleLanguage: Languages.English } => "sub",
            { AudioLanguage: Languages.English, SubtitleLanguage: _ } => "dub",
            { AudioLanguage: Languages.Japanese, SubtitleLanguage: "" } => "raw",
            _ => throw new NotSupportedException(streamType.ToString())
        };
    }
}