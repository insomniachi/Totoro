using Totoro.Plugins.Anime.Models;

namespace Totoro.Plugins.Anime.AllAnime;

internal static class Extensions
{
    public static string ConvertToTranslationType(this StreamType streamType)
    {
        return streamType switch
        {
            StreamType.EnglishSubbed => "sub",
            StreamType.EnglishDubbed => "dub",
            StreamType.Raw => "raw",
            _ => throw new NotSupportedException(streamType.ToString())
        };
    }
}