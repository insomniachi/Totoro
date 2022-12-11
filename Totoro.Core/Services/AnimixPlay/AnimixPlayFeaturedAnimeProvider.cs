namespace Totoro.Core.Services.AnimixPlay;

public class AnimixPlayFeaturedAnimeProvider : IFeaturedAnimeProvider
{
    private readonly HttpClient _httpClient;

    public AnimixPlayFeaturedAnimeProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(Constants.UserAgent);
    }

    public IObservable<IEnumerable<FeaturedAnime>> GetFeaturedAnime()
    {
        return Observable.Create<IEnumerable<FeaturedAnime>>(async observer =>
        {
            var response = await _httpClient.GetAsync("https://animixplay.to/assets/s/featured.json");

            if (!response.IsSuccessStatusCode)
            {
                observer.OnError(new Exception("response does not contain success code"));
            }

            var stream = await response.Content.ReadAsStreamAsync();
            observer.OnNext(JsonSerializer.Deserialize(stream, FeaturedAnimeCollectionSerializerContext.Default.ListFeaturedAnime));
            observer.OnCompleted();
        });
    }
}
