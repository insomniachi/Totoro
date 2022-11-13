using System.Reactive.Concurrency;
using MonoTorrent;

namespace Totoro.Core.ViewModels
{
    public class DownloadViewModel : NavigatableViewModel
    {
        private readonly SourceCache<ShanaProjectCatalogItem, long> _searchResultsCache = new(x => x.Id);
        private readonly ReadOnlyObservableCollection<ShanaProjectCatalogItem> _searchResults;
        private readonly ObservableAsPropertyHelper<List<ShanaProjectDownloadableContent>> _downloadableContent;

        public DownloadViewModel(IShanaProjectService shanaProjectService,
                                 ITorrentsService torrentsSerivce)
        {
            TorrentsSerivce = torrentsSerivce;

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

            this.ObservableForProperty(x => x.SelectedSeries, x => x)
                .Select(series => series.Id)
                .SelectMany(id => shanaProjectService.Search(id).ToListAsync().AsTask())
                .Do(dc => dc.ForEach(x => x.Download.Subscribe( async _ => await Download(x))))
                .ToProperty(this, nameof(DownloadableContent), out _downloadableContent, scheduler: RxApp.MainThreadScheduler);

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
            Torrent torrent = Torrent.Load(new Uri(content.Url), "temp.torrent");
            await TorrentsSerivce.Download(torrent, Path.Combine("Downloads", content.Title));
            ShowDownloads = true;
        }

        [Reactive] public string Term { get; set; }
        [Reactive] public ShanaProjectCatalogItem SelectedSeries { get; set; }
        [Reactive] public bool ShowDownloads { get; set; }
        public List<ShanaProjectDownloadableContent> DownloadableContent => _downloadableContent?.Value ?? new();
        public ReadOnlyObservableCollection<ShanaProjectCatalogItem> SearchResults => _searchResults;

        public ITorrentsService TorrentsSerivce { get; }
    }
}
