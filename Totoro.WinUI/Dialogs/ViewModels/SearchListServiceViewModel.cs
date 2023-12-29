using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Dialogs.ViewModels
{
    public class SearchListServiceViewModel : DialogViewModel
    {
        [Reactive] public string SearchText { get; set; }
        [Reactive] public List<AnimeModel> SearchResults { get; set; }
        [Reactive] public AnimeModel SelectedAnime { get; set; }
        [Reactive] public bool CanAddToList { get; private set; }
        [ObservableAsProperty] public bool HaveSelectedAnime { get; }


        public SearchListServiceViewModel(IAnimeServiceContext animeServiceContext,
                                          ITrackingServiceContext trackingServiceContext,
                                          INavigationService navigationService)
        {
            this.WhenAnyValue(x => x.SearchText)
                .Where(x => x is { Length: > 2 })
                .Throttle(TimeSpan.FromSeconds(1))
                .SelectMany(animeServiceContext.GetAnime)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(list => SearchResults = list.ToList());

            this.WhenAnyValue(x => x.SelectedAnime)
                .Select(x => x is not null && x.Tracking is null)
                .Subscribe(x => CanAddToList = x);

            this.WhenAnyValue(x => x.SelectedAnime)
                .WhereNotNull()
                .SelectMany(x => x.WhenAnyValue(y => y.Tracking))
                .Subscribe(tracking => CanAddToList = tracking is null);
            
            this.WhenAnyValue(x => x.SelectedAnime)
                .Select(x => x is not null)
                .ToPropertyEx(this, x => x.HaveSelectedAnime);

        }
    }
}
