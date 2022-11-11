using System.Net;
using MonoTorrent;
using MonoTorrent.Client;

namespace Totoro.Core.ViewModels
{
    public class DownloadViewModel : NavigatableViewModel
    {
        private readonly ITorrentsService _torrentsSerivce;
        private readonly SourceCache<ShanaProjectCatalogItem, long> _searchResultsCache = new(x => x.Id);
        private readonly ReadOnlyObservableCollection<ShanaProjectCatalogItem> _searchResults;
        private readonly ObservableAsPropertyHelper<List<ShanaProjectDownloadableContent>> _downloadableContent;

        public DownloadViewModel(IShanaProjectService shanaProjectService,
                                 ITorrentsService torrentsSerivce)
        {
            _torrentsSerivce = torrentsSerivce;

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
                .Do(dc => dc.ForEach(x => x.Download.Subscribe( _ => Download(x))))
                .ToProperty(this, nameof(DownloadableContent), out _downloadableContent, scheduler: RxApp.MainThreadScheduler);

        }

        private void Download(ShanaProjectDownloadableContent content)
        {
            Torrent torrent = Torrent.Load(new Uri(content.Url), "temp.torrent");
            _torrentsSerivce.Download(torrent, Path.Combine("Downloads", content.Title));
        }

        [Reactive] public string Term { get; set; }
        [Reactive] public ShanaProjectCatalogItem SelectedSeries { get; set; }
        public List<ShanaProjectDownloadableContent> DownloadableContent => _downloadableContent?.Value ?? new();
        public ReadOnlyObservableCollection<ShanaProjectCatalogItem> SearchResults => _searchResults;
    }
}
