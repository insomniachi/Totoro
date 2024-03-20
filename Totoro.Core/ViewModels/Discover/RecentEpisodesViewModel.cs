using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;

namespace Totoro.Core.ViewModels.Discover
{
    public class RecentEpisodesViewModel : NavigatableViewModel
    {
        private readonly SourceCache<IAiredAnimeEpisode, string> _episodesCache = new(x => (x.Url + x.EpisodeString));
        private readonly ReadOnlyObservableCollection<IAiredAnimeEpisode> _episodes;
        private readonly INavigationService _navigationService;

        public RecentEpisodesViewModel(IPluginFactory<AnimeProvider> providerFacotory,
                                       ISettings settings,
                                       INavigationService navigationService)
        {
            _navigationService = navigationService;

            _episodesCache
                .Connect()
                .RefCount()
                .Filter(this.WhenAnyValue(x => x.FilterText).Select(FilterByTitle))
                .Sort(SortExpressionComparer<IAiredAnimeEpisode>.Ascending(x => _episodesCache.Items.IndexOf(x)))
                .Bind(out _episodes)
                .DisposeMany()
                .Subscribe()
                .DisposeWith(Garbage);

            Plugins = providerFacotory.Plugins.ToList();
            SelectedProvider = providerFacotory.Plugins.FirstOrDefault(x => x.Name == settings.DefaultProviderType);

            SelectEpisode = ReactiveCommand.CreateFromTask<IAiredAnimeEpisode>(OnEpisodeSelected);
            LoadMore = ReactiveCommand.CreateFromTask(LoadMoreEpisodes, this.WhenAnyValue(x => x.IsEpisodesLoading).Select(x => !x));

            this.WhenAnyValue(x => x.SelectedProvider)
                .WhereNotNull()
                .Do(provider =>
                {
                    CardWidth = provider.Name is "anime-pahe" or "marin" ? 480 : 190; // animepahe image is thumbnail
                    DontUseImageEx = settings.DefaultProviderType is "yugen-anime"; // using imagex for yugen is crashing
                })
                .Select(providerInfo => providerFacotory.CreatePlugin(providerInfo.Name))
                .ToPropertyEx(this, x => x.Provider);

            this.WhenAnyValue(x => x.Provider)
                .WhereNotNull()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Do(_ => _episodesCache.Clear())
                .SelectMany(_ => LoadPage(1).ToObservable())
                .Subscribe();
        }

        [Reactive] public string FilterText { get; set; }
        [Reactive] public bool IsEpisodesLoading { get; set; }
        [Reactive] public PluginInfo SelectedProvider { get; set; }
        [Reactive] public double CardWidth { get; set; }
        [Reactive] public int TotalPages { get; set; } = 1;
        [Reactive] public bool DontUseImageEx { get; private set; }
        [ObservableAsProperty] public AnimeProvider Provider { get; }


        public List<PluginInfo> Plugins { get; }
        public ReadOnlyObservableCollection<IAiredAnimeEpisode> Episodes => _episodes;

        public ICommand LoadMore { get; }
        public ICommand SelectEpisode { get; }


        private async Task LoadMoreEpisodes()
        {
            await LoadPage(TotalPages + 1);
            TotalPages++;
        }


        private async Task LoadPage(int page)
        {
            if (Provider is not { AiredAnimeEpisodeProvider : not null})
            {
                return;
            }

            IsEpisodesLoading = true;
            try
            {
                var results = await Provider.AiredAnimeEpisodeProvider.GetRecentlyAiredEpisodes(page).ToListAsync();
                _episodesCache.AddOrUpdate(results);
            }
            catch(Exception ex) 
            {
                RxApp.DefaultExceptionHandler.OnError(ex);
            }
            finally
            {
                IsEpisodesLoading = false;
            }
        }

        private Task OnEpisodeSelected(IAiredAnimeEpisode episode)
        {
            var navigationParameters = new Dictionary<string, object>
            {
                [WatchViewModelParamters.EpisodeInfo] = episode,
                [WatchViewModelParamters.Provider] = SelectedProvider.Name
            };

            _navigationService.NavigateTo<WatchViewModel>(parameter: navigationParameters);

            return Task.CompletedTask;
        }

        private static Func<IAiredAnimeEpisode, bool> FilterByTitle(string title) => (IAiredAnimeEpisode ae) => string.IsNullOrEmpty(title) || ae.Title.Contains(title, StringComparison.CurrentCultureIgnoreCase);
    }
}
