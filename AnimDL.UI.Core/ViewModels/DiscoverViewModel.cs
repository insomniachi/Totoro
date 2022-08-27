using System.Text.Json.Serialization;
using MalApi;
using MalApi.Interfaces;

namespace AnimDL.UI.Core.ViewModels;

public class DiscoverViewModel : NavigatableViewModel, IHaveState
{
    public DiscoverViewModel()
    {

        Observable
            .Timer(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                if (Featured.Count == 0)
                {
                    SelectedIndex = 0;
                    return;
                }

                if (SelectedIndex == Featured.Count - 1)
                {
                    SelectedIndex = 0;
                    return;
                }

                SelectedIndex++;
            });
    }

    [Reactive] public ObservableCollection<FeaturedAnime> Featured { get; set; }
    [Reactive] public int SelectedIndex { get; set; }

    public void RestoreState(IState state)
    {
        Featured = state.GetValue<ObservableCollection<FeaturedAnime>>(nameof(Featured));
    }

    public async Task SetInitialState()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.81 Safari/537.36 Edg/104.0.1293.54");
        var stream = await client.GetStreamAsync("https://animixplay.to/assets/s/featured.json");
        Featured = await JsonSerializer.DeserializeAsync<ObservableCollection<FeaturedAnime>>(stream);
    }

    public void StoreState(IState state)
    {
        state.AddOrUpdate(Featured);
    }
}

public class FeaturedAnime
{
    public string Id => Url?.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(1).FirstOrDefault();
    public string[] GenresArray => Genres?.Split(",");

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("img")]
    public string Image { get; set; }

    [JsonPropertyName("genre")]
    public string Genres { get; set; }

    [JsonPropertyName("desc")]
    public string Description { get; set; }
}
