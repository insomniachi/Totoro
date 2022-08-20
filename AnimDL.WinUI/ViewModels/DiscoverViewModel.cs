using MalApi;
using MalApi.Interfaces;

namespace AnimDL.WinUI.ViewModels;

public class DiscoverViewModel : NavigatableViewModel, IHaveState
{
    private readonly IMalClient _client;

    public DiscoverViewModel(IMalClient client)
    {
        _client = client;

        Observable
            .Timer(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                if (TopAiring.Count == 0)
                {
                    SelectedIndex = 0;
                    return;
                }

                if (SelectedIndex == TopAiring.Count - 1)
                {
                    SelectedIndex = 0;
                    return;
                }

                SelectedIndex++;
            });
    }

    [Reactive] public ObservableCollection<Anime> TopAiring { get; set; } = new();
    [Reactive] public int SelectedIndex { get; set; }

    public void RestoreState(IState state)
    {
    }

    public async Task SetInitialState()
    {
        var result = await _client.Anime()
                                  .Top(AnimeRankingType.Airing)
                                  .WithLimit(10)
                                  .Find();

        result.Data.ForEach(async x =>
        {
            TopAiring.Add(await _client.Anime()
                .WithId(x.Anime.Id)
                .WithField(x => x.Background)
                .WithField(x => x.Pictures)
                .WithField(x => x.Synopsis)
                .WithField(x => x.Genres)
                .Find());
        });
    }

    public void StoreState(IState state)
    {
    }
}
