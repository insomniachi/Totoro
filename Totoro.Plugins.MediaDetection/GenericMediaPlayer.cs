using System.Diagnostics;
using System.Reactive.Linq;
using FlaUI.Core.AutomationElements;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.Plugins.MediaDetection;

public abstract class GenericMediaPlayer : ReactiveObject, INativeMediaPlayer
{
    public Process? Process { get; private set; }
    protected Window? _window;

    public bool GetTitleFromWindow { get; protected set; } = false;
    [Reactive] public string Title { get; set; } = string.Empty;
    public IObservable<string> TitleChanged { get; }

    public GenericMediaPlayer()
    {
        TitleChanged = this.WhenAnyValue(x => x.Title).DistinctUntilChanged();
    }

    public virtual Task<string> GetTitle()
    {
        if (GetTitleFromWindow && _window is not null)
        {
            return Task.FromResult(_window.Title);
        }
        else if (Process is not null)
        {
            return ParseFromWindowTitle(Process.MainWindowTitle);
        }

        return Task.FromResult(string.Empty);
    }

    public async Task Initialize(Window window)
    {
        _window = window;
        Process = Process.GetProcessById(window.Properties.ProcessId);
        Title = await GetTitle();
    }

    protected virtual Task<string> ParseFromWindowTitle(string windowTitle) => Task.FromResult(windowTitle);

    public abstract void Dispose();
}
