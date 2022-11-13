using MonoTorrent.Client;

namespace Totoro.Core.Models
{
    public class TorrentModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<double> _progress;

        public TorrentModel(TorrentManager torrentManager)
        {
            Downloader = torrentManager;
            Name = torrentManager.Torrent.Name;
            
            Observable
                .FromEventPattern<EventHandler<PieceHashedEventArgs>, PieceHashedEventArgs>(x => torrentManager.PieceHashed += x, x => torrentManager.PieceHashed -= x)
                .Select(_ => Math.Round(torrentManager.Progress, 2))
                .ToProperty(this, x => x.Progress, out _progress, scheduler: RxApp.MainThreadScheduler, initialValue: 0);

            this.WhenAnyValue(x => x.Progress)
                .Where(p => p == 100)
                .Subscribe(_ => IsCompleted = true);

        }

        public string Name { get; set; }
        public double Progress => _progress.Value;
        [Reactive]public bool IsCompleted { get; set; }
        public TorrentManager Downloader { get; }
    }
}