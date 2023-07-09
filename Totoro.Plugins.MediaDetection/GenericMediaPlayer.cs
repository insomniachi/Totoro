using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.Plugins.MediaDetection;

public abstract class GenericMediaPlayer : INativeMediaPlayer
{
    public Process? Process { get; private set; }
    protected Window? _window;

    public bool GetTitleFromWindow { get; protected set; } = false;

    public void Dispose() { }

    public virtual string GetTitle()
    {
        if(GetTitleFromWindow && _window is not null)
        {
            return _window.Title;
        }
        else if(Process is not null)
        {
            return ParseFromWindowTitle(Process.MainWindowTitle);
        }

        return string.Empty;
    }

    public void Initialize(Window window)
    {
        _window = window;
        Process = Process.GetProcessById(window.Properties.ProcessId);
    }

    protected virtual string ParseFromWindowTitle(string windowTitle) => windowTitle;
}
