namespace Totoro.Plugins.Torrents.AnimeTosho;

internal static class Config
{
    internal static string Url { get; set; } = @"https://mirror.animetosho.org/";
    internal static Filter Filter { get; set; } = Filter.TrustedOnly;
    internal static Sort Sort { get; set; } = Sort.NewestFirst;
}
