using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.Plugins.MediaDetection.Generic;

public class Mpv : GenericMediaPlayer, ICanLaunch
{
    private bool _hasCustomTitle;
    private string? _customTitle;

    protected override string ParseFromWindowTitle(string windowTitle)
    {
        return windowTitle.Replace("- mpv", string.Empty);
    }

    public override string GetTitle()
    {
        if(_hasCustomTitle)
        {
            return _customTitle!;
        }

        return base.GetTitle();
    }

    public void Launch(string title, string url)
    {
        _hasCustomTitle = true;
        _customTitle = title;
        Application.Launch(MpvConfig.FileName, $"{url} --title=\"{title}\" --fs");
    }
}

public sealed class MpcHc : INativeMediaPlayer, ICanLaunch
{
    private Window? _window;
    private bool _hasCustomTitle;
    private string? _customTitle;

    public Process? Process { get; private set; }

    public void Dispose() { }

    public string GetTitle()
    {
        if(_hasCustomTitle)
        {
            return _customTitle!;
        }

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

    public void Launch(string title, string url)
    {
        _hasCustomTitle = true;
        _customTitle = title;
        Application.Launch(MpcConfig.FileName, $"{url} /fullscreen");
    }
}
