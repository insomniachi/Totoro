using System.Text.RegularExpressions;
using Flurl.Http;

namespace Totoro.Plugins.Anime.Marin;

internal partial class Config
{
    internal static string Url { get; set; } = "https://marin.moe/";

    internal static CookieJar GetCookieJar()
    {
        if (_jar is not null)
        {
            return _jar;
        }

        _jar = new CookieJar();
        _jar.AddOrReplace("__ddg2_", "YW5pbWRsX3NheXNfaGkNCg.", Url);
        return _jar;
    }

    internal static async ValueTask<string> GetInertiaVersion()
    {
        if(!string.IsNullOrEmpty(_inertiaVersion))
        {
            return _inertiaVersion;
        }

        var response = await Url.WithCookies(GetCookieJar()).GetStringAsync();
        _inertiaVersion = VersionRegex().Match(response).Groups[1].Value;
        return _inertiaVersion;
    }

    private static string? _inertiaVersion;
    private static CookieJar? _jar;

    [GeneratedRegex("version&quot;:&quot;(.+?)&quot;")]
    private static partial Regex VersionRegex();
}
