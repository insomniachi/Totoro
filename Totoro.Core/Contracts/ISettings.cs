using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.ViewModels;

namespace Totoro.Core.Contracts;

public interface ISettings : INotifyPropertyChanged
{
    bool PreferSubs { get; set; }
    string DefaultProviderType { get; set; }
    string DefaultTorrentTrackerType { get; set; }
    string DefaultMediaPlayer { get; set; }
    bool UseDiscordRichPresense { get; set; }
    bool ShowTimeRemainingOnDiscordRichPresense { get; set; }
    int TimeRemainingWhenEpisodeCompletesInSeconds { get; set; }
    int OpeningSkipDurationInSeconds { get; set; }
    Guid AniSkipId { get; }
    bool ContributeTimeStamps { get; set; }
    LogLevel MinimumLogLevel { get; set; }
    bool AutoUpdate { get; set; }
    ListServiceType DefaultListService { get; set; }
    string HomePage { get; set; }
    bool AllowSideLoadingPlugins { get; set; }
    StreamQualitySelection DefaultStreamQualitySelection { get; set; }
    bool IncludeNsfw { get; set; }
    bool EnterFullScreenWhenPlaying { get; set; }
    DebridServiceType DebridServiceType { get; set; }
    string PremiumizeApiKey { get; set; }
    AdvanceTorrentSearchOptions TorrentSearchOptions { get; set; }
    MediaPlayerType MediaPlayerType { get; set; }
    bool PreBufferTorrents { get; set; }
    bool AutoRemoveWatchedTorrents { get; set; }
    string UserTorrentsDownloadDirectory { get; set; }
    bool AutoDownloadTorrents { get; set; }
    string AnimeCardClickAction { get; set; }
    int SmallSkipAmount { get; set; }
    bool MediaDetectionEnabled { get; set; }
    bool OnlyDetectMediaInLibraryFolders { get; set; }
    ObservableCollection<string> LibraryFolders { get; set; }
    StartupOptions StartupOptions { get; set; }
    DisplayMode ListDisplayMode { get; set; }
    GridViewSettings UserListGridViewSettings { get; set; }
    public string DefaultMangaProviderType { get; set; }
    public bool SkipFillers { get; set; }
}

public class StartupOptions : ReactiveObject
{
    [Reactive] public bool MinimizeToTrayOnClose { get; set; } = false;
    [Reactive] public bool StartMinimizedToTray { get; set; } = false;
    [Reactive] public bool RunOnStartup { get; set; } = false;
}

public class GridViewSettings : ReactiveObject
{
    [Reactive] public int MaxNumberOfColumns { get; set; } = -1;
    [Reactive] public double SpacingBetweenItems { get; set; } = 6;
    [Reactive] public double ItemHeight { get; set; } = 380;
    [Reactive] public double DesiredWidth { get; set; } = 240;
}