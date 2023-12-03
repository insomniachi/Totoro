using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Identifiers;
using FlaUI.UIA3.Converters;
using FlaUI.UIA3.EventHandlers;
using Splat;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.Plugins.MediaDetection;

public class ProcessWatcher : IEnableLogger
{
    private readonly List<Process> _processes = new();
    private readonly Subject<INativeMediaPlayer> _mediaPlayerDetected = new();
    private readonly Subject<int> _mediaPlayerClosed = new();
    private readonly FlaUI.UIA3.UIA3Automation _automation = new();
    private readonly AutomationElement _desktop;
    private readonly UIA3AutomationEventHandler _automationEventHandler;
    private readonly object _lock = new object();

    public ProcessWatcher()
    {
        _desktop = _automation.GetDesktop();
        _automationEventHandler = new(_desktop.FrameworkAutomationElement, _automation.EventLibrary.Window.WindowOpenedEvent, OnWindowOpened);
        DetectMediaProcess();
    }

    public void Enable()
    {
        foreach (var item in _desktop.FindAllChildren(cb => cb.ByControlType(ControlType.Window)).Select(x => x.AsWindow()))
        {
            OnWindowOpened(item, EventId.NotSupportedByFramework);
        }
        _automation.NativeAutomation.AddAutomationEventHandler(_automation.EventLibrary.Window.WindowOpenedEvent.Id,
                                                       _desktop.ToNative(),
                                                       (Interop.UIAutomationClient.TreeScope)TreeScope.Descendants,
                                                       null,
                                                       _automationEventHandler);
    }

    public void Disable()
    {
        _automation.NativeAutomation.RemoveAutomationEventHandler(_automation.EventLibrary.Window.WindowOpenedEvent.Id, _desktop.ToNative(), _automationEventHandler);
    }

    public IObservable<INativeMediaPlayer> MediaPlayerDetected => _mediaPlayerDetected;
    public IObservable<int> MediaPlayerClosed => _mediaPlayerClosed;

    private void OnWindowOpened(AutomationElement element, EventId id)
    {
        var window = element.AsWindow();
        var pid = window.Properties.ProcessId.Value;
        var process = Process.GetProcessById(pid);
        var name = process.ProcessName;

        if (process.MainWindowTitle == "Media Player") // win 11 media player
        {
            name = "Microsoft.Media.Player";
            process = Process.GetProcessesByName("Microsoft.Media.Player").First();
        }

        if (_processes.FirstOrDefault(x => x.Id == process.Id) is { })
        {
            return;
        }

        if (PluginFactory<INativeMediaPlayer>.Instance.Plugins.FirstOrDefault(x => x.Name == name) is { } pluginInfo)
        {
            if (PluginFactory<INativeMediaPlayer>.Instance.CreatePlugin(name) is { } player)
            {
                _processes.Add(process);
                this.Log().Info($"Media Player ({name}) detected");
                player.Initialize(window);
                _mediaPlayerDetected.OnNext(player);
            }
        }
    }

    public void RemoveProcess(Process process)
    {
        lock (_lock)
        {
            _processes.Remove(process);
        }
    }

    private void DetectMediaProcess()
    {
        List<Process> exited = new();

        lock (_lock)
        {
            exited = _processes.Where(x => x.HasExited).ToList();
        }

        exited.ForEach(x =>
        {
            _processes.Remove(x);
            _mediaPlayerClosed.OnNext(x.Id);
        });

        Observable.Timer(TimeSpan.FromSeconds(5)).Subscribe(_ => DetectMediaProcess());
    }
}
