using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.Plugins.MediaDetection.Generic;

public class Mpv : GenericMediaPlayer
{
    protected override string ParseFromWindowTitle(string windowTitle)
    {
        return windowTitle.Replace("- mpv", string.Empty);
    }
}

public sealed class MpcHc : INativeMediaPlayer
{
    private Window? _window;

    public MpcHc()
    {
    }

    public Process? Process { get; private set; }

    public void Dispose() { }

    public string GetTitle()
    {
        var title = _window!.Title;
        while(title == "Media Player Classic Home Cinema")
        {
            title = _window!.Title;
            Thread.Sleep(100);
        }

        return title;
    }

    public void Initialize(Window window)
    {
        _window = window;
        Process = Process.GetProcessById(window.Properties.ProcessId);
    }
}
