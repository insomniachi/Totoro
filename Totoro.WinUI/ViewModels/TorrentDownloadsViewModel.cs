namespace Totoro.WinUI.ViewModels
{
    public class TorrentDownloadsViewModel : ReactiveObject
    {
        public TorrentDownloadsViewModel(ITorrentsService torrentsService)
        {
            TorrentsService = torrentsService;

            Observable
                .Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))
                .Subscribe(_ => this.RaisePropertyChanged(nameof(TorrentsService)));
        }

        public ITorrentsService TorrentsService { get; }
    }
}
