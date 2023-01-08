using Humanizer;
using MonoTorrent.Client;

namespace Totoro.Core.Models
{
    public class TorrentModel : ReactiveObject
    {
        private readonly ObservableAsPropertyHelper<double> _progress;
        private readonly ObservableAsPropertyHelper<string> _downloadSpeed;

        public TorrentModel(TorrentManager torrentManager)
        {
            Downloader = torrentManager;
            Name = torrentManager.Torrent.Name;

            Observable
                .FromEventPattern<EventHandler<PieceHashedEventArgs>, PieceHashedEventArgs>(x => torrentManager.PieceHashed += x, x => torrentManager.PieceHashed -= x)
                .Select(_ => Math.Round(torrentManager.Progress, 2))
                .ToProperty(this, x => x.Progress, out _progress, scheduler: RxApp.MainThreadScheduler, initialValue: 0);

            Observable
                .Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))
                .Select(_ => $"{torrentManager.Monitor.DownloadSpeed.Bytes().Humanize()}/s")
                .ToProperty(this, x => x.DownloadSpeed, out _downloadSpeed, scheduler: RxApp.MainThreadScheduler, initialValue: "");

            this.WhenAnyValue(x => x.Progress)
                .Where(p => p == 100)
                .Subscribe(_ => IsCompleted = true);

        }

        public string Name { get; set; }
        public double Progress => _progress.Value;
        public string DownloadSpeed => _downloadSpeed.Value;
        [Reactive] public bool IsCompleted { get; set; }
        public TorrentManager Downloader { get; }
    }
}