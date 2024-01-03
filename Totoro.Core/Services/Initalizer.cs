using System.Reactive.Subjects;
using Totoro.Plugins.Contracts;

namespace Totoro.Core.Services;

public class Initalizer : IInitializer
{
    private readonly IPluginManager _pluginManager;
    private readonly IKnownFolders _knownFolders;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IRssDownloader _rssDownloader;
    private readonly IConnectivityService _connectivityService;
    private readonly PluginOptionsStorage _pluginOptionsStorage;
    private readonly IOfflineAnimeIdService _offlineAnimeIdService;
    private readonly IUpdateService _updateService;
    private readonly IResumePlaybackService _playbackStateStorage;
    private readonly ITorrentEngine _torrentEngine;
    private readonly Subject<Unit> _onShutDown = new();

    public Initalizer(IPluginManager pluginManager,
                      IKnownFolders knownFolders,
                      IUpdateService updateService,
                      IResumePlaybackService playbackStateStorage,
                      ITorrentEngine torrentEngine,
                      ILocalSettingsService localSettingsService,
                      IRssDownloader rssDownloader,
                      IConnectivityService connectivityService,
                      PluginOptionsStorage pluginOptionsStorage,
                      IOfflineAnimeIdService offlineAnimeIdService)
    {
        _pluginManager = pluginManager;
        _knownFolders = knownFolders;
        _localSettingsService = localSettingsService;
        _rssDownloader = rssDownloader;
        _connectivityService = connectivityService;
        _pluginOptionsStorage = pluginOptionsStorage;
        _offlineAnimeIdService = offlineAnimeIdService;
        _updateService = updateService;
        _playbackStateStorage = playbackStateStorage;
        _torrentEngine = torrentEngine;
    }

    public async Task Initialize()
    {
        if (_connectivityService.IsConnected)
        {
            await _torrentEngine.TryRestoreState();
            await _rssDownloader.Initialize();
        }
        _pluginOptionsStorage.Initialize();
        _offlineAnimeIdService.Initialize();
        RemoveObsoleteSettings();
    }

    public async Task ShutDown()
    {
        _onShutDown.OnNext(Unit.Default);
        _updateService.ShutDown();
        _playbackStateStorage.SaveState();
        _rssDownloader.SaveState();
        await _torrentEngine.SaveState();
    }

    private void RemoveObsoleteSettings()
    {
        foreach (var key in Settings.GetObsoleteKeys())
        {
            _localSettingsService.RemoveSetting(key);
        }
    }

    public IObservable<Unit> OnShutDown => _onShutDown;
}
