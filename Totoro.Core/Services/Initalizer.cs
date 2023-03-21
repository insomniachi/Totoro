using Akavache;
using MalApi;
using Microsoft.Extensions.Logging;
using Totoro.Core.Services.AniList;
using Totoro.Core.Torrents;

namespace Totoro.Core.Services;

public class Initalizer : IInitializer
{
    private readonly IPluginManager _pluginManager;
    private readonly IKnownFolders _knownFolders;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IUpdateService _updateService;
    private readonly IPlaybackStateStorage _playbackStateStorage;
    private readonly Func<ILegacyLocalSettingsService> _legacyLocalSettingsServiceFactory;

    public Initalizer(IPluginManager pluginManager,
                      IKnownFolders knownFolders,
                      ILocalSettingsService localSettingsService,
                      IUpdateService updateService,
                      IPlaybackStateStorage playbackStateStorage,
                      Func<ILegacyLocalSettingsService> legacyLocalSettingsServiceFactory)
    {
        _pluginManager = pluginManager;
        _knownFolders = knownFolders;
        _localSettingsService = localSettingsService;
        _updateService = updateService;
        _playbackStateStorage = playbackStateStorage;
        _legacyLocalSettingsServiceFactory = legacyLocalSettingsServiceFactory;
    }

    public async Task Initialize()
    {
        await _pluginManager.Initialize();
        MigrateLegacySettings();
        RemoveObsoleteSettings();
    }

    public void ShutDown()
    {
        _updateService.ShutDown();
        _playbackStateStorage.StoreState();
        BlobCache.LocalMachine.Flush();
        BlobCache.Shutdown().Wait();
    }

    private void RemoveObsoleteSettings()
    {
        _localSettingsService.RemoveSetting("DebridOptions");

        foreach (var key in Settings.GetObsoleteKeys())
        {
            _localSettingsService.RemoveSetting(key);
        }
    }

    private void MigrateLegacySettings()
    {
        var legacySettingsPath = Path.Combine(_knownFolders.ApplicationDataLegacy, @"LocalSettings.json");
        if (!File.Exists(legacySettingsPath))
        {
            return;
        }

        var legacyService = _legacyLocalSettingsServiceFactory();
        File.Delete(legacySettingsPath);
        Directory.Delete(_knownFolders.ApplicationDataLegacy, true);

        SetValue<Dictionary<string, long>>("LocalMedia");
        SetValue<OAuthToken>("MalToken");
        SetValue<Dictionary<long, Dictionary<int, double>>>("Recents");
        SetValue<string>("Nyaa");
        SetValue<AniListAuthToken>("AniListToken");

        SetValue<bool>(nameof(ISettings.PreferSubs));
        SetValue<string>(nameof(ISettings.DefaultProviderType));
        SetValue<bool>(nameof(ISettings.UseDiscordRichPresense));
        SetValue<bool>(nameof(ISettings.ContributeTimeStamps));
        SetValue<LogLevel>(nameof(ISettings.MinimumLogLevel));
        SetValue<bool>(nameof(ISettings.AutoUpdate));
        SetValue<ListServiceType>(nameof(ISettings.DefaultListService));
        SetValue<string>(nameof(ISettings.HomePage));
        SetValue<bool>(nameof(ISettings.AllowSideLoadingPlugins));
        SetValue<StreamQualitySelection>(nameof(ISettings.DefaultStreamQualitySelection));
        SetValue<bool>(nameof(ISettings.IncludeNsfw));
        SetValue<bool>(nameof(ISettings.EnterFullScreenWhenPlaying));
        SetValue<DebridServiceType>(nameof(ISettings.DebridServiceType));
        SetValue<TorrentProviderType>(nameof(ISettings.TorrentProviderType));


        void SetValue<T>(string key)
        {
            _localSettingsService.SaveSetting<T>(key, legacyService.ReadSetting<T>(key).Wait());
        }
    }
}
