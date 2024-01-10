namespace Totoro.Plugins.Torrents.Nya;

public enum Filter
{
    None = 0,
    NoRemakes = 1,
    TrustedOnly = 2
}

public enum SortBy
{
    Seeders,
    Leechers,
    Date
}

public enum SortDirection
{
    Ascending,
    Descending
}

public enum Category
{
    None,
    Anime,
    AnimeEnglishTranslated,
    AnimeNonEnglishTranslated,
    AnimeRaw
}
