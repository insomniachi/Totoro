using FlaUI.Core;
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
