using System.Reflection;
using System.Text.Json.Serialization;
using Splat;
using Totoro.Core.Torrents;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Totoro.Core.ViewModels;

internal class SettingsModel : ReactiveObject, ISettings
{
    public SettingsModel(ILocalSettingsService localSettingsService,
                         IDiscordRichPresense dRpc)
    {
        ElementTheme = ElementTheme.Dark;

        localSettingsService.ReadSetting(Settings.DefaultProviderType).Subscribe(x => DefaultProviderType = x);
        localSettingsService.ReadSetting(Settings.PreferSubs).Subscribe(x => PreferSubs = x);
        localSettingsService.ReadSetting(Settings.UseDiscordRichPresense).Subscribe(x => UseDiscordRichPresense = x);
        localSettingsService.ReadSetting(Settings.TimeRemainingWhenEpisodeCompletesInSeconds).Subscribe(x => TimeRemainingWhenEpisodeCompletesInSeconds = x);
        localSettingsService.ReadSetting(Settings.OpeningSkipDurationInSeconds).Subscribe(x => OpeningSkipDurationInSeconds = x);
        localSettingsService.ReadSetting(Settings.ContributeTimeStamps).Subscribe(x => ContributeTimeStamps = x);
        localSettingsService.ReadSetting(Settings.MinimumLogLevel).Subscribe(x => MinimumLogLevel = x);
        localSettingsService.ReadSetting(Settings.AutoUpdate).Subscribe(x => AutoUpdate = x);
        localSettingsService.ReadSetting(Settings.DefaultListService).Subscribe(x => DefaultListService = x);
        localSettingsService.ReadSetting(Settings.HomePage).Subscribe(x => HomePage = x);
        localSettingsService.ReadSetting(Settings.AllowSideLoadingPlugins).Subscribe(x => AllowSideLoadingPlugins = x);
        localSettingsService.ReadSetting(Settings.DefaultStreamQualitySelection).Subscribe(x => DefaultStreamQualitySelection = x);
        localSettingsService.ReadSetting(Settings.IncludeNsfw).Subscribe(x => IncludeNsfw = x);
        localSettingsService.ReadSetting(Settings.EnterFullScreenWhenPlaying).Subscribe(x => EnterFullScreenWhenPlaying = x);
        localSettingsService.ReadSetting(Settings.DebridServiceType).Subscribe(x => DebridServiceType = x);
        localSettingsService.ReadSetting(Settings.TorrentProviderType).Subscribe(x => TorrentProviderType = x);
        localSettingsService.ReadSetting(Settings.AniSkipId).Subscribe(x => AniSkipId = x);
        localSettingsService.ReadSetting(Settings.PremiumizeApiKey).Subscribe(x => PremiumizeApiKey = x);

        if (UseDiscordRichPresense && !dRpc.IsInitialized)
        {
            dRpc.Initialize();
            dRpc.SetPresence();
        }

        Changed
            .Select(x => GetType().GetProperty(x.PropertyName))
            .Where(x => x.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .Subscribe(propInfo =>
            {
                var value = propInfo.GetValue(this);
                this.Log().Debug($"""Setting Changed "{propInfo.Name}" => {value}""");
                localSettingsService.SaveSetting(propInfo.Name, value);
            });

        this.ObservableForProperty(x => x.UseDiscordRichPresense, x => x)
            .Where(x => x && !dRpc.IsInitialized)
            .Subscribe(value =>
            {
                dRpc.Initialize();
                dRpc.SetPresence();
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
}
