namespace Totoro.Plugins.Anime.Models;

public static class Languages
{
    public const string English = @"Engilish";
    public const string Italian = @"Italian";
    public const string Spanish = @"Spanish";
    public const string Japanese = @"Japanese";
    public const string German = @"German";
}

public record StreamType(string AudioLanguage, string SubtitleLanguage)
{
    public static StreamType Raw() => new(Languages.Japanese, "");
    public static StreamType Subbed(string langauge) => new(Languages.Japanese, langauge);
    public static StreamType Dubbed(string language, string subtitleLangauge = "") => new(language, subtitleLangauge);
}