namespace Totoro.Core.Contracts;

public interface IFeaturedAnimeProvider
{
    IObservable<IEnumerable<FeaturedAnime>> GetFeaturedAnime();
}
