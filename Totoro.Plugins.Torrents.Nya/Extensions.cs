namespace Totoro.Plugins.Torrents.Nya;

internal static class Extensions
{
    internal static string ToQueryParam(this Category category)
    {
        return category switch
        {
            Category.None => "0_0",
            Category.Anime => "1_0",
            Category.AnimeEnglishTranslated => "1_2",
            Category.AnimeNonEnglishTranslated => "1_3",
            Category.AnimeRaw => "1_4",
            _ => "0_0"
        };
    }

    internal static string ToQueryString(this SortDirection direction)
    {
        return direction switch
        {
            SortDirection.Ascending => "asc",
            SortDirection.Descending => "desc",
            _ => "asc"
        };
    }
}
