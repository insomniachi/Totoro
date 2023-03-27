using System.Reflection;
using System.Text.Json.Serialization;
using Splat;
using Totoro.Core.Torrents;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Totoro.Core.ViewModels;

internal class SettingsModel : ReactiveObject, ISettings
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IDiscordRichPresense _dRpc;

    public SettingsModel(ILocalSettingsService localSettingsService,
                         IDiscordRichPresense dRpc)
    {
        _localSettingsService = localSettingsService;
        _dRpc = dRpc;

        DefaultProviderType = localSettingsService.ReadSetting(Settings.DefaultProviderType);
        PreferSubs = localSettingsService.ReadSetting(Settings.PreferSubs);
        UseDiscordRichPresense = localSettingsService.ReadSetting(Settings.UseDiscordRichPresense);
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
        TorrentProviderType = localSettingsService.ReadSetting(Settings.TorrentProviderType);
        AniSkipId = localSettingsService.ReadSetting(Settings.AniSkipId);
        PremiumizeApiKey = localSettingsService.ReadSetting(Settings.PremiumizeApiKey);
        TorrentSearchOptions = localSettingsService.ReadSetting(Settings.TorrentSearchOptions);
        MediaPlayerType = localSettingsService.ReadSetting(Settings.MediaPlayerType);

        ObserveChanges();
    }

    public void ObserveChanges()
    {
        if (UseDiscordRichPresense && !_dRpc.IsInitialized)
        {
            _dRpc.Initialize();
            _dRpc.SetPresence();
        }

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
                _dRpc.SetPresence();
            });

        ObserveObject(TorrentSearchOptions, Settings.TorrentSearchOptions);
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

    [Reactive] public ElementTheme ElementTheme { get; set; }
    [Reactive] public bool PreferSubs { get; set; }
    [Reactive] public string DefaultProviderType { get; set; }
    [Reactive] public bool UseDiscordRichPresense { get; set; }
    [Reactive] public int TimeRemainingWhenEpisodeCompletesInSeconds { get; set; }
    [Reactive] public int OpeningSkipDurationInSeconds { get; set; }
    [Reactive] public bool ContributeTimeStamps { get; set; }
    [Reactive] public DefaultUrls DefaultUrls { get; set; }
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
    [Reactive] public TorrentProviderType TorrentProviderType { get; set; }
    [Reactive] public string PremiumizeApiKey { get; set; }
    [Reactive] public AdvanceTorrentSearchOptions TorrentSearchOptions { get; set; }
    [Reactive] public MediaPlayerType MediaPlayerType { get; set; }
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