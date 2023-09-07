using System.Text.RegularExpressions;

namespace Totoro.Plugins.Helpers;

public partial class RegexHelper
{
    [GeneratedRegex(@"(\d+)")]
    public static partial Regex IntegerRegex();

    [GeneratedRegex(@"(?:https://)myanimelist\.net/anime/(\d+)")]
    public static partial Regex MyAnimeListLinkRegex();

    [GeneratedRegex(@"(?:https://)?anilist\.co/anime/(\d+)")]
    public static partial Regex AnilistLinkRegex();
}
