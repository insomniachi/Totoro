using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Splat;
using Totoro.Plugins.MediaDetection.Contracts;
using static PInvoke.User32;

namespace Totoro.Plugins.MediaDetection;

public class ProcessWatcher : IEnableLogger
{
    private readonly List<Process> _processes = new();
    private readonly Subject<INativeMediaPlayer> _mediaPlayerDetected = new();
    private readonly Subject<int> _mediaPlayerClosed = new();
    
    public ProcessWatcher()
    {
        Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(5))
             .Subscribe(_ => DetectMediaProcess());
    }
    
    public IObservable<INativeMediaPlayer> MediaPlayerDetected => _mediaPlayerDetected;
    public IObservable<int> MediaPlayerClosed => _mediaPlayerClosed;

    private void DetectMediaProcess()
    {
        if(Process.GetProcesses().Where(x => x.ProcessName == "vlc").ToList() is { Count: >0} list)
        {
            ;
        }

        foreach (var process in Process.GetProcesses())
        {
            if (process.Id == Environment.ProcessId)
            {
                continue;
            }

            if (_processes.Find(x => x.Id == process.Id) is { })
            {
                continue;
            }

            List<Process> exited = new List<Process>();
            foreach (var p in _processes)
            {
                if (p.HasExited)
                {
                    exited.Add(p);
                    _mediaPlayerClosed.OnNext(p.Id);
                }
            }

            exited.ForEach(x => _processes.Remove(x));

            if (PluginFactory<INativeMediaPlayer>.Instance.Plugins.FirstOrDefault(x => x.Name == process.ProcessName) is { } pluginInfo)
            {
                _processes.Add(process);
                if (PluginFactory<INativeMediaPlayer>.Instance.CreatePlugin(process.ProcessName) is { } player)
                {
                    this.Log().Info($"Media Player ({process.ProcessName}) detected");
                    player.Initialize(process.Id);
                    _mediaPlayerDetected.OnNext(player);
                }
            }
        }
    }
}
