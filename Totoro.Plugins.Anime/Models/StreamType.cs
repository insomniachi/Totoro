using System.Diagnostics.CodeAnalysis;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.Anime.Models;

public static class Languages
{
    public const string English = @"English";
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

    public override string ToString()
    {
        if (AudioLanguage != Languages.Japanese)
        {
            return @$"{AudioLanguage} Dubbed";
        }
        else if (!string.IsNullOrEmpty(SubtitleLanguage))
        {
            return @$"{SubtitleLanguage} Subbed";
        }
        else
        {
            return @"Raw";
        }
    }

    public static StreamType Parse(string s)
    {
        if(s.Equals(@"raw", StringComparison.OrdinalIgnoreCase))
        {
            return Raw();
        }

        var split = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (split[1].Equals("dubbed", StringComparison.OrdinalIgnoreCase))
        {
            return Dubbed(split[0]);
        }
        else if(split[1].Equals("subbed", StringComparison.OrdinalIgnoreCase))
        {
            return Subbed(split[0]);
        }

        throw new ArgumentException(null, nameof(s));
    }

    public static bool TryParse([NotNullWhen(true)] string? s, [MaybeNullWhen(false)] out StreamType result)
    {
        result = null;

        if(s is null)
        {
            return false;
        }

        if (s.Equals(@"raw", StringComparison.OrdinalIgnoreCase))
        {
            result = Raw();
            return true;
        }

        var split = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if(split.Length != 2)
        {
            return false;
        }


        if (split[1].Equals("dubbed", StringComparison.OrdinalIgnoreCase))
        {
            result = Dubbed(split[0]);
            return true;
        }
        else if (split[1].Equals("subbed", StringComparison.OrdinalIgnoreCase))
        {
            result = Subbed(split[0]);
            return true;
        }

        return false;
    }
}

public static class PluginOptionsExtensions
{
    public static StreamType GetStreamType(this PluginOptions options, string name, StreamType defaultValue)
    {
        return options.GetValue(name, defaultValue, StreamType.Parse);
    }
}