using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;

namespace Plugin.AllAnime;

public class SearchResult : ICatalogItem, IHaveImage, IHaveSeason, IHaveRating
{
    required public string Season { get; init; }
    required public string Year { get; init; }
    required public string Image { get; init; }
    required public string Rating { get; init; }
    required public string Title { get; init; }
    required public string Url { get; init; }
}
