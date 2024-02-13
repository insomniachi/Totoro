using System.Diagnostics;
using System.Reactive.Subjects;
using System.Windows.Forms;
using FlaUI.Core.AutomationElements;
using ReactiveUI;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.Plugins.MediaDetection.Win11MediaPlayer;

internal partial class MediaPlayer : ReactiveObject, INativeMediaPlayer
{
    private Window? _mainWindow;

    public IObservable<string> TitleChanged { get; }
    public Process? Process { get; private set; }
    public string Title { get; set; } = string.Empty;

    public MediaPlayer()
    {
        TitleChanged = this.WhenAnyValue(x => x.Title);
    }

    public void Dispose() { }

    public Task<string> GetTitle()
    {
        if (_mainWindow is null)
        {
            return Task.FromResult(string.Empty);
        }

        var element = _mainWindow.FindFirstDescendant(cb => cb.ByAutomationId("mediaItemTitle"));
        while (element is null)
        {
            element = _mainWindow.FindFirstDescendant(cb => cb.ByAutomationId("mediaItemTitle"));
        }

        return Task.FromResult(element.Name);
    }


    public async Task Initialize(Window window)
    {
        _mainWindow = window;
        Process = Process.GetProcessesByName("Microsoft.Media.Player").First();
        Title = await GetTitle();
    }
}
