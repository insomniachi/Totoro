using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts.Optional;

namespace Totoro.Plugins.Anime.AnimePahe;

internal class SearchResult : ICatalogItem, IHaveSeason, IHaveImage, IHaveStatus
{
    required public string Season { get; init; }
    required public string Status { get; init; }
    required public string Image { get; init; } 
    required public string Year { get; init; }
    required public string Title { get; init; }
    required public string Url { get; init; }
}


