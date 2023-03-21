using Microsoft.Extensions.Logging;
using Totoro.Core.Torrents;

namespace Totoro.Core.Contracts;

public interface ISettings
{
    ElementTheme ElementTheme { get; set; }
    bool PreferSubs { get; set; }
    string DefaultProviderType { get; set; }
    bool UseDiscordRichPresense { get; set; }
    int TimeRemainingWhenEpisodeCompletesInSeconds { get; set; }
    int OpeningSkipDurationInSeconds { get; set; }
    Guid AniSkipId { get; }
    bool ContributeTimeStamps { get; set; }
    LogLevel MinimumLogLevel { get; set; }
    bool AutoUpdate { get; set; }
    ListServiceType? DefaultListService { get; set; }
    string HomePage { get; set; }
    bool AllowSideLoadingPlugins { get; set; }
    StreamQualitySelection DefaultStreamQualitySelection { get; set; }
    bool IncludeNsfw { get; set; }
    bool EnterFullScreenWhenPlaying { get; set; }
    DebridServiceType DebridServiceType { get; set; }
    TorrentProviderType TorrentProviderType { get; set; }
}

public class DefaultUrls : ReactiveObject
{
    [Reactive] public string GogoAnime { get; set; }
    [Reactive] public string Tenshi { get; set; }
    [Reactive] public string Yugen { get; set; }
    [Reactive] public string AnimePahe { get; set; }
    [Reactive] public string AllAnime { get; set; }
}
