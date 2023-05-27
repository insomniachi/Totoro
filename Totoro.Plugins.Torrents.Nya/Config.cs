namespace Totoro.Plugins.Torrents.Nya;

internal static class Config
{
    public static string Url { get; set; } = "https://nyaa.ink/";
    public static Filter Filter { get; set; } = Filter.TrustedOnly;
    public static Category Category { get; set; } = Category.Anime;
    public static SortBy SortBy { get; set; } = SortBy.Seeders;
    public static SortDirection SortDirection { get; set; } = SortDirection.Descending;
}
