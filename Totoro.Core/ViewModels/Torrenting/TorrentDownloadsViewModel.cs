namespace Totoro.Core.ViewModels.Torrenting;

public class TorrentDownloadsViewModel : NavigatableViewModel
{
    public TorrentDownloadsViewModel(ITorrentEngine torrentEngine)
    {
        torrentEngine
            .TorrentRemoved
            .Select(name => EngineTorrents.FirstOrDefault(x => x.Name == name))
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x =>
            {
                EngineTorrents.Remove(x);
                x.Dispose();
            });
        
        torrentEngine
            .TorrentAdded
            .Subscribe(x => EngineTorrents.Add(new TorrentManagerModel(torrentEngine, x)));

        EngineTorrents = new(torrentEngine.TorrentManagers.Select(x => new TorrentManagerModel(torrentEngine, x)));
    }

    public ObservableCollection<TorrentManagerModel> EngineTorrents { get; }
}
