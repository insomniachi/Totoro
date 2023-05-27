namespace Totoro.Plugins.Torrents.Nya;

internal enum Filter
{
    None = 0,
    NoRemakes = 1,
    TrustedOnly = 2
}

internal enum SortBy
{
    Seeders,
    Leechers
}

internal enum SortDirection
{
    Ascending,
    Descending
}

internal enum Category
{
    None,
    Anime,
    AnimeEnglishTranslated,
    AnimeNonEnglishTranslated,
    AnimeRaw
}
