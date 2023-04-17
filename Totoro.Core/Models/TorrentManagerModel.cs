using System.Reactive.Concurrency;
using Humanizer;
using MonoTorrent.Client;

namespace Totoro.Core.Models;

public sealed class TorrentManagerModel : ReactiveObject, IDisposable
{
    private IDisposable _subscription;

    public TorrentManagerModel(ITorrentEngine engine,
                               TorrentManager torrentManager)
    {
        Manager = torrentManager;
        torrentManager.TorrentStateChanged += TorrentManager_TorrentStateChanged;

        var canDelete = this.WhenAnyValue(x => x.CanDelete);
        Remove = ReactiveCommand.CreateFromTask(() => engine.RemoveTorrent(Name, false), canDelete);
        Delete = ReactiveCommand.CreateFromTask(() => engine.RemoveTorrent(Name, true), canDelete);
        Resume = ReactiveCommand.CreateFromTask(torrentManager.StartAsync, this.WhenAnyValue(x => x.CanResume));
        Pause = ReactiveCommand.CreateFromTask(torrentManager.PauseAsync, this.WhenAnyValue(x => x.CanPause));
        Stop = ReactiveCommand.CreateFromTask(torrentManager.StopAsync, this.WhenAnyValue(x => x.CanStop));

        UpdateCommandStates(torrentManager.State);

        this.WhenAnyValue(x => x.Complete)
            .Where(x => x)
            .Subscribe(_ => _subscription?.Dispose());
    }

    [Reactive] public bool CanResume { get; private set; }
    [Reactive] public bool CanDelete { get; private set; }
    [Reactive] public bool CanPause { get; private set; }
    [Reactive] public bool Complete { get; private set; }
    [Reactive] public bool CanStop { get; private set; }
    [Reactive] public string Progress { get; private set; }
    [Reactive] public string Speed { get; private set; }

    public TorrentManager Manager { get; }
    public string Name => Manager.Torrent.Name;
    
    public ICommand Remove { get; }
    public ICommand Delete { get; }
    public ICommand Resume { get; }
    public ICommand Pause { get; }
    public ICommand Stop { get; }

    private void TorrentManager_TorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
    {
        RxApp.MainThreadScheduler.Schedule(() => UpdateCommandStates(e.NewState));
    }

    private void UpdateCommandStates(TorrentState state)
    {
        if(state == TorrentState.Downloading)
        {
            Monitor();
        }

        CanResume = state is TorrentState.Paused or TorrentState.Stopped && !Manager.Complete;
        CanDelete = state is TorrentState.Stopped or TorrentState.Paused;
        CanPause = state is TorrentState.Downloading;
        CanStop = state is not (TorrentState.Stopping or TorrentState.Stopped);
        Complete = Manager.Complete;
        Progress = Manager.Progress.ToString("N2");
    }

    private void Monitor()
    {
        if (_subscription is not null)
        {
            return;
        }

        _subscription = Observable
            .Timer(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                Progress = Manager.Progress.ToString("N2");
                Speed = $"({Manager.Monitor.DownloadSpeed.Bytes().Humanize()}/s)";
                Complete = Manager.Complete;
            }); 
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _subscription = null;
    }
}
