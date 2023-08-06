using System.Reflection;
using System.Text.Json.Serialization;
using Splat;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Totoro.Core.ViewModels;

internal class SettingsModel : ReactiveObject, ISettings
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IDiscordRichPresense _dRpc;

    public SettingsModel(ILocalSettingsService localSettingsService,
                         IDiscordRichPresense dRpc,
                         IKnownFolders knownFolders)
    {
        _localSettingsService = localSettingsService;
        _dRpc = dRpc;

        DefaultProviderType = localSettingsService.ReadSetting(Settings.DefaultProviderType);
        PreferSubs = localSettingsService.ReadSetting(Settings.PreferSubs);
        UseDiscordRichPresense = localSettingsService.ReadSetting(Settings.UseDiscordRichPresense);
        ShowTimeRemainingOnDiscordRichPresense = localSettingsService.ReadSetting(Settings.ShowTimeRemainingOnDiscordRichPresense);
        TimeRemainingWhenEpisodeCompletesInSeconds = localSettingsService.ReadSetting(Settings.TimeRemainingWhenEpisodeCompletesInSeconds);
        OpeningSkipDurationInSeconds = localSettingsService.ReadSetting(Settings.OpeningSkipDurationInSeconds);
        ContributeTimeStamps = localSettingsService.ReadSetting(Settings.ContributeTimeStamps);
        MinimumLogLevel = localSettingsService.ReadSetting(Settings.MinimumLogLevel);
        AutoUpdate = localSettingsService.ReadSetting(Settings.AutoUpdate);
        DefaultListService = localSettingsService.ReadSetting(Settings.DefaultListService);
        HomePage = localSettingsService.ReadSetting(Settings.HomePage);
        AllowSideLoadingPlugins = localSettingsService.ReadSetting(Settings.AllowSideLoadingPlugins);
        DefaultStreamQualitySelection = localSettingsService.ReadSetting(Settings.DefaultStreamQualitySelection);
        IncludeNsfw = localSettingsService.ReadSetting(Settings.IncludeNsfw);
        EnterFullScreenWhenPlaying = localSettingsService.ReadSetting(Settings.EnterFullScreenWhenPlaying);
        DebridServiceType = localSettingsService.ReadSetting(Settings.DebridServiceType);
        DefaultTorrentTrackerType = localSettingsService.ReadSetting(Settings.DefaultTorrentTrackerType);
        AniSkipId = localSettingsService.ReadSetting(Settings.AniSkipId);
        PremiumizeApiKey = localSettingsService.ReadSetting(Settings.PremiumizeApiKey);
        TorrentSearchOptions = localSettingsService.ReadSetting(Settings.TorrentSearchOptions);
        MediaPlayerType = localSettingsService.ReadSetting(Settings.MediaPlayerType);
        PreBufferTorrents = localSettingsService.ReadSetting(Settings.PreBufferTorrents);
        UserTorrentsDownloadDirectory = localSettingsService.ReadSetting(nameof(UserTorrentsDownloadDirectory), knownFolders.Torrents);
        AutoDownloadTorrents = localSettingsService.ReadSetting(Settings.AutoDownloadTorrents);
        AutoRemoveWatchedTorrents = localSettingsService.ReadSetting(Settings.AutoRemoveWatchedTorrents);
        AnimeCardClickAction = localSettingsService.ReadSetting(Settings.AnimeCardClickAction);
        SmallSkipAmount = localSettingsService.ReadSetting(Settings.SmallSkipAmount);
        DefaultMediaPlayer = localSettingsService.ReadSetting(Settings.DefaultMediaPlayer);
        MediaDetectionEnabled = localSettingsService.ReadSetting(Settings.MediaDetectionEnabled);
        OnlyDetectMediaInLibraryFolders = localSettingsService.ReadSetting(Settings.OnlyDetectMediaInLibraryFolders);
        LibraryFolders = localSettingsService.ReadSetting(Settings.LibraryFolders);
        StartupOptions = localSettingsService.ReadSetting(Settings.StartupOptions);
        ListDisplayMode = localSettingsService.ReadSetting(Settings.ListDisplayMode);
        UserListGridViewSettings = localSettingsService.ReadSetting(Settings.UserListGridViewSettings);

        if (UseDiscordRichPresense && !_dRpc.IsInitialized)
        {
            _dRpc.Initialize();
        }

        ObserveChanges();
    }

    public void ObserveChanges()
    {
        Changed
            .Select(x => GetType().GetProperty(x.PropertyName))
            .Where(x => x.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(propInfo =>
            {
                var value = propInfo.GetValue(this);
                this.Log().Debug($"""Setting Changed "{propInfo.Name}" => {value}""");
                _localSettingsService.SaveSetting(propInfo.Name, value);
            });

        this.ObservableForProperty(x => x.UseDiscordRichPresense, x => x)
            .Where(x => x && !_dRpc.IsInitialized)
            .Subscribe(value =>
            {
                _dRpc.Initialize();
            });

        LibraryFolders
            .ToObservableChangeSet()
            .Subscribe(_ => _localSettingsService.SaveSetting(Settings.LibraryFolders, LibraryFolders));

        ObserveObject(TorrentSearchOptions, Settings.TorrentSearchOptions);
        ObserveObject(StartupOptions, Settings.StartupOptions);
        ObserveObject(UserListGridViewSettings, Settings.UserListGridViewSettings);
    }

    private void ObserveObject<T>(T target, Key<T> key)
        where T : ReactiveObject
    {
        target
            .Changed
            .Select(x => target.GetType().GetProperty(x.PropertyName))
            .Where(x => x.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(propInfo =>
            {
                var value = propInfo.GetValue(target);
                this.Log().Debug($"""Setting Changed "{key.Name}.{propInfo.Name}" => {value}""");
                _localSettingsService.SaveSetting(key, target);
            });
    }

    [Reactive] public bool PreferSubs { get; set; }
    [Reactive] public string DefaultProviderType { get; set; }
    [Reactive] public bool UseDiscordRichPresense { get; set; }
    [Reactive] public bool ShowTimeRemainingOnDiscordRichPresense { get; set; }
    [Reactive] public int TimeRemainingWhenEpisodeCompletesInSeconds { get; set; }
    [Reactive] public int OpeningSkipDurationInSeconds { get; set; }
    [Reactive] public bool ContributeTimeStamps { get; set; }
    [Reactive] public LogLevel MinimumLogLevel { get; set; }
    [Reactive] public bool AutoUpdate { get; set; }
    [Reactive] public ListServiceType DefaultListService { get; set; }
    [Reactive] public Guid AniSkipId { get; set; }
    [Reactive] public string HomePage { get; set; }
    [Reactive] public bool AllowSideLoadingPlugins { get; set; }
    [Reactive] public StreamQualitySelection DefaultStreamQualitySelection { get; set; }
    [Reactive] public bool IncludeNsfw { get; set; }
    [Reactive] public bool EnterFullScreenWhenPlaying { get; set; }
    [Reactive] public DebridServiceType DebridServiceType { get; set; }
    [Reactive] public string PremiumizeApiKey { get; set; }
    [Reactive] public AdvanceTorrentSearchOptions TorrentSearchOptions { get; set; }
    [Reactive] public MediaPlayerType MediaPlayerType { get; set; }
    [Reactive] public bool PreBufferTorrents { get; set; }
    [Reactive] public bool AutoRemoveWatchedTorrents { get; set; }
    [Reactive] public string UserTorrentsDownloadDirectory { get; set; }
    [Reactive] public bool AutoDownloadTorrents { get; set; }
    [Reactive] public string AnimeCardClickAction { get; set; }
    [Reactive] public string DefaultTorrentTrackerType { get; set; }
    [Reactive] public int SmallSkipAmount { get; set; }
    [Reactive] public string DefaultMediaPlayer { get; set; }
    [Reactive] public bool MediaDetectionEnabled { get; set; }
    [Reactive] public bool OnlyDetectMediaInLibraryFolders { get; set; }
    public ObservableCollection<string> LibraryFolders { get; set; }
    [Reactive] public StartupOptions StartupOptions { get; set; }
    [Reactive] public DisplayMode ListDisplayMode { get; set; }
    [Reactive] public GridViewSettings UserListGridViewSettings { get; set; }
}


public class AdvanceTorrentSearchOptions : ReactiveObject
{
    [Reactive] public bool IsEnabled { get; set; }
    [Reactive] public string Subber { get; set; }
    [Reactive] public string Quality { get; set; }

    public static AdvanceTorrentSearchOptions Default => new()
    {
        IsEnabled = false,
        Subber = "SubsPlease",
        Quality = "1080",
    };
}