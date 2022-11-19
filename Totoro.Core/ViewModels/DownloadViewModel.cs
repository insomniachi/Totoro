using System.Reactive.Concurrency;

namespace Totoro.Core.ViewModels
{
    public class DownloadViewModel : NavigatableViewModel
    {
        private readonly SourceCache<ShanaProjectCatalogItem, long> _searchResultsCache = new(x => x.Id);
        private readonly ReadOnlyObservableCollection<ShanaProjectCatalogItem> _searchResults;
        private readonly ObservableAsPropertyHelper<ShanaProjectPage> _shanaProjectPage;
        private CompositeDisposable _downloadSubscriptions = new();

        public DownloadViewModel(IShanaProjectService shanaProjectService,
                                 ITorrentsService torrentsSerivce)
        {
            TorrentsSerivce = torrentsSerivce;

            NextPage = ReactiveCommand.Create(() => ++Page, this.WhenAnyValue(x => x.ShanaProjectPage).Select(page => page is { HasNextPage: true }));
            PreviousPage = ReactiveCommand.Create(() => --Page, this.WhenAnyValue(x => x.ShanaProjectPage).Select(page => page is { HasPreviousPage: true }));

            _searchResultsCache
                .Connect()
                .RefCount()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out _searchResults)
                .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnNext)
                .DisposeWith(Garbage);

            this.ObservableForProperty(x => x.Term, x => x)
                .Where(term => term is { Length: >3 })
                .Throttle(TimeSpan.FromMilliseconds(250))
                .ObserveOn(RxApp.TaskpoolScheduler)
                .SelectMany(term => shanaProjectService.Search(term))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(result => _searchResultsCache.EditDiff(result, (first, second) => first.Id == second.Id))
                .DisposeWith(Garbage);

            var seriesChanged = this.WhenAnyValue(x => x.SelectedSeries)
                .WhereNotNull()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Do(_ => IsLoading = true)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .SelectMany(series => shanaProjectService.Search(series.Id, 1))
                .Do(_ => { _downloadSubscriptions.Dispose(); _downloadSubscriptions = new(); })
                .Do(dc => dc.DownloadableContents.ForEach(x => x.Download.Subscribe(async _ => await Download(x)).DisposeWith(_downloadSubscriptions)))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Do(_ => IsLoading = false);

            var pageChanged = this.WhenAnyValue(x => x.Page)
                .Where(_ => SelectedSeries is not null)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Do(_ => IsLoading = true)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .SelectMany(page => shanaProjectService.Search(SelectedSeries.Id, page))
                .Do(_ => { _downloadSubscriptions.Dispose(); _downloadSubscriptions = new(); })
                .Do(dc => dc.DownloadableContents.ForEach(x => x.Download.Subscribe(async _ => await Download(x)).DisposeWith(_downloadSubscriptions)))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Do(_ => IsLoading = false);

            Observable.Merge(seriesChanged, pageChanged)
                .ToProperty(this, nameof(ShanaProjectPage), out _shanaProjectPage, scheduler: RxApp.MainThreadScheduler);

            torrentsSerivce.ActiveDownlaods.ActOnEveryObject(_ => { }, _ =>
            {
                if (!torrentsSerivce.ActiveDownlaods.Any())
                {
                    RxApp.MainThreadScheduler.Schedule(() => ShowDownloads = false);
                }
            });
        }

        private async Task Download(ShanaProjectDownloadableContent content)
        {
            await TorrentsSerivce.Download(content);
            ShowDownloads = true;
        }

        [Reactive] public string Term { get; set; }
        [Reactive] public ShanaProjectCatalogItem SelectedSeries { get; set; }
        [Reactive] public bool ShowDownloads { get; set; }
        [Reactive] public int Page { get; set; } = 1;
        [Reactive] public bool IsLoading { get; set; }
        public ShanaProjectPage ShanaProjectPage => _shanaProjectPage?.Value ?? new();
        public ReadOnlyObservableCollection<ShanaProjectCatalogItem> SearchResults => _searchResults;
        public ICommand NextPage { get; }
        public ICommand PreviousPage { get; }
        public ITorrentsService TorrentsSerivce { get; }
    }
}
