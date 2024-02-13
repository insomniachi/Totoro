using System.Diagnostics;
using System.Reactive.Linq;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using ReactiveUI;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Plugins.MediaDetection.Generic;

public sealed class Mpv : GenericMediaPlayer, ICanLaunch
{
    private bool _hasCustomTitle;
    private string? _customTitle;
    private Application? _application;

    protected override Task<string> ParseFromWindowTitle(string windowTitle)
    {
        return Task.FromResult(windowTitle.Replace("- mpv", string.Empty));
    }

    public override Task<string> GetTitle()
    {
        if (_hasCustomTitle)
        {
            return Task.FromResult(_customTitle!);
        }

        return base.GetTitle();
    }

    public Task Launch(string title, string url)
    {
        _hasCustomTitle = true;
        _customTitle = title;
        _application = Application.Launch(ConfigManager<MpvConfig>.Current.FileName, $"{url} --title=\"{title}\" --fs");
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _application?.Dispose();
    }
}

public sealed class MpcHc : INativeMediaPlayer, ICanLaunch
{
    private Window? _window;
    private bool _hasCustomTitle;
    private string? _customTitle;

    public Process? Process { get; private set; }
    public string Title { get; set; } = string.Empty;
    public IObservable<string> TitleChanged { get; }
    public MpcHc()
    {
        TitleChanged = this.WhenAnyValue(x => x.Title).DistinctUntilChanged();
    }

    public void Dispose() { }

    public async Task<string> GetTitle()
    {
        if (_hasCustomTitle)
        {
            return _customTitle!;
        }

        var title = _window!.Title;
        while (title == "Media Player Classic Home Cinema")
        {
            title = _window!.Title;
            await Task.Delay(100);
        }

        return title;
    }

    public async Task Initialize(Window window)
    {
        _window = window;
        Process = Process.GetProcessById(window.Properties.ProcessId);
        Title = await GetTitle();
    }

    public async Task Launch(string title, string url)
    {
        _hasCustomTitle = true;
        _customTitle = title;
        Application.Launch(ConfigManager<MpcConfig>.Current.FileName, $"{url} /fullscreen");
        Title = await GetTitle();
    }
}
