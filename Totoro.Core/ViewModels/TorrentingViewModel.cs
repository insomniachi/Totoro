using Totoro.Core.ViewModels.Torrenting;

namespace Totoro.Core.ViewModels;

public enum SortMode
{
    Date,
    Seeders
}

public class TorrentingViewModel : NavigatableViewModel
{
    private readonly SourceList<PivotItemModel> _sectionsList = new();
    private readonly ReadOnlyObservableCollection<PivotItemModel> _sections;
    private readonly int _downloadsSectionIndex = 1;

    public TorrentingViewModel(ITorrentEngine torrentEngine)
    {
        _sectionsList
            .Connect()
            .RefCount()
            .AutoRefresh(x => x.Visible)
            .Filter(x => x.Visible)
            .Bind(out _sections)
            .Subscribe()
            .DisposeWith(Garbage);

        torrentEngine
            .TorrentAdded
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                var downloadItem = _sectionsList.Items.ElementAt(_downloadsSectionIndex);
                downloadItem.Visible = true;
                SelectedSection = downloadItem;

            }, RxApp.DefaultExceptionHandler.OnError);

        torrentEngine
            .TorrentRemoved
            .Where(x => !torrentEngine.TorrentManagers.Any())
            .Subscribe(_ =>
            {
                var downloadItem = _sectionsList.Items.ElementAt(_downloadsSectionIndex);
                downloadItem.Visible = false;
                SelectedSection = _sectionsList.Items.First(x => x.Visible);

            }, RxApp.DefaultExceptionHandler.OnError);

        _sectionsList.Add(new PivotItemModel
        {
            Header = "Search",
            ViewModel = typeof(SearchTorrentViewModel)
        });
        _sectionsList.Add(new PivotItemModel
        {
            Header = "Downloads",
            ViewModel = typeof(TorrentDownloadsViewModel),
            Visible = torrentEngine.TorrentManagers.Any()
        });
    }

    [Reactive] public PivotItemModel SelectedSection { get; set; }
    public ReadOnlyObservableCollection<PivotItemModel> Sections => _sections;
}
