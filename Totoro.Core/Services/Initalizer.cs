using System.Reactive.Subjects;

namespace Totoro.Core.Services;

public class Initalizer : IInitializer
{
    private readonly IPluginManager _pluginManager;
    private readonly IKnownFolders _knownFolders;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IUpdateService _updateService;
    private readonly IPlaybackStateStorage _playbackStateStorage;
    private readonly ITorrentEngine _torrentEngine;
    private readonly Subject<Unit> _onShutDown = new();

    public Initalizer(IPluginManager pluginManager,
                      IKnownFolders knownFolders,
                      IUpdateService updateService,
                      IPlaybackStateStorage playbackStateStorage,
                      ITorrentEngine torrentEngine,
                      ILocalSettingsService localSettingsService)
    {
        _pluginManager = pluginManager;
        _knownFolders = knownFolders;
        _localSettingsService = localSettingsService;
        _updateService = updateService;
        _playbackStateStorage = playbackStateStorage;
        _torrentEngine = torrentEngine;
    }

    public async Task Initialize()
    {
        await _pluginManager.Initialize();
        await _torrentEngine.TryRestoreState();
        //MigrateLegacySettings();
        RemoveObsoleteSettings();
    }

    public async Task ShutDown()
    {
        _onShutDown.OnNext(Unit.Default);
        _updateService.ShutDown();
        _playbackStateStorage.StoreState();
        await _torrentEngine.ShutDown();
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
