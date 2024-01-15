using System.Reactive.Concurrency;
using System.Reflection;
using MonoTorrent.Client;
using Splat;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Manga;
using Totoro.Plugins.Torrents.Contracts;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Totoro.Core.ViewModels;

public class SettingsViewModel : NavigatableViewModel, IHandleNavigation
{
    private readonly ITrackingServiceContext _trackingServiceContext;
    public const string DefaultAnimeProvider = "gogo-anime";
    public const string DefaultTorrentTracker = "nya";
    public const string DefaultMangaProvider = "manga-dex";

    [Reactive] public bool IsMalConnected { get; set; }
    [Reactive] public bool IsAniListConnected { get; set; }
    [Reactive] public bool IsSimklConnected { get; set; }
    [Reactive] public PluginInfo SelectedProvider { get; set; }
    [Reactive] public PluginInfo SelectedMangaProvider { get; set; }
    [Reactive] public PluginInfo SelectedTracker { get; set; }
    [Reactive] public PluginInfo SelectedMediaPlayer { get; set; }
    [Reactive] public ElementTheme Theme { get; set; }
    [ObservableAsProperty] public bool HasInactiveTorrents { get; }
    public ObservableCollection<TorrentManager> InactiveTorrents { get; }

    public ISettings Settings { get; }
    public Version Version { get; }
    public Version ScrapperVersion { get; }
    public IEnumerable<PluginInfo> ProviderTypes => PluginFactory<AnimeProvider>.Instance.Plugins;
    public IEnumerable<PluginInfo> TrackerTypes => PluginFactory<ITorrentTracker>.Instance.Plugins;
    public IEnumerable<PluginInfo> MangaProviderTypes => PluginFactory<MangaProvider>.Instance.Plugins;
    public IEnumerable<DisplayMode> ListDisplayModes { get; } = Enum.GetValues<DisplayMode>().Cast<DisplayMode>().Take(2).ToList();
    public List<LogLevel> LogLevels { get; } = [LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical];
    public List<ListServiceType> ServiceTypes { get; } = [ListServiceType.MyAnimeList, ListServiceType.AniList, ListServiceType.Simkl];
    public List<string> HomePages { get; } = ["Discover", "My List"];
    public List<string> AnimeActions { get; } = ["Watch", "Info"];
    public ICommand AuthenticateCommand { get; }
    public ICommand ShowAbout { get; }
    public ICommand Navigate { get; }
    public ICommand EditUserTorrentDirectory { get; }

    public BreadCrumbBarModel BreadCrumbBar { get; } = new("Settings");

    public SettingsViewModel(ISettings settings,
                             ITrackingServiceContext trackingServiceContext,
                             IViewService viewService,
                             IUpdateService updateService,
                             IThemeSelectorService themeSelectorService,
                             ITorrentEngine torrentEngine)
    {
        _trackingServiceContext = trackingServiceContext;

        Settings = settings;
        Version = Assembly.GetEntryAssembly().GetName().Version;
        SelectedProvider = PluginFactory<AnimeProvider>.Instance.Plugins.FirstOrDefault(x => x.Name == settings.DefaultProviderType)
            ?? PluginFactory<AnimeProvider>.Instance.Plugins.FirstOrDefault(x => x.Name == DefaultAnimeProvider);
        SelectedTracker = PluginFactory<ITorrentTracker>.Instance.Plugins.FirstOrDefault(x => x.Name == settings.DefaultTorrentTrackerType)
            ?? PluginFactory<ITorrentTracker>.Instance.Plugins.FirstOrDefault(x => x.Name == DefaultAnimeProvider);
        SelectedMangaProvider = PluginFactory<MangaProvider>.Instance.Plugins.FirstOrDefault(x => x.Name == settings.DefaultMangaProviderType)
            ?? PluginFactory<MangaProvider>.Instance.Plugins.FirstOrDefault(x => x.Name == DefaultMangaProvider);
        AuthenticateCommand = ReactiveCommand.CreateFromTask<ListServiceType>(viewService.Authenticate);
        ShowAbout = ReactiveCommand.CreateFromTask(async () =>
        {
            var currentInfo = await updateService.GetCurrentVersionInfo();
            if (currentInfo is null)
            {
                return;
            }
            await viewService.Information($"{currentInfo.Version}", currentInfo.Body);
        });
        Navigate = ReactiveCommand.Create<string>(BreadCrumbBar.BreadCrumbs.Add);
        EditUserTorrentDirectory = ReactiveCommand.Create(async () =>
        {
            var folder = await viewService.BrowseFolder();
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            RxApp.MainThreadScheduler.Schedule(() => Settings.UserTorrentsDownloadDirectory = folder);
        });

        InactiveTorrents = new(torrentEngine.TorrentManagers.Where(x => x.State == TorrentState.Stopped));
        Theme = themeSelectorService.Theme;

        this.ObservableForProperty(x => x.SelectedProvider, x => x)
            .Subscribe(provider => settings.DefaultProviderType = provider.Name);

        this.ObservableForProperty(x => x.SelectedTracker, x => x)
            .Subscribe(tracker => settings.DefaultTorrentTrackerType = tracker.Name);

        this.WhenAnyValue(x => x.Theme)
            .Subscribe(themeSelectorService.SetTheme);

        InactiveTorrents
            .ToObservableChangeSet()
            .Select(_ => InactiveTorrents.Count > 0)
            .ToPropertyEx(this, x => x.HasInactiveTorrents);

        trackingServiceContext
            .Authenticated
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateConnectionStatus());

        torrentEngine
            .TorrentRemoved
            .Log(this, "Torrent Removed")
            .Select(name => InactiveTorrents.FirstOrDefault(x => x.Torrent.Name == name))
            .WhereNotNull()
            .Do(name => InactiveTorrents.Remove(name))
            .Subscribe();

        UpdateConnectionStatus();
    }

    public bool CanHandle() => BreadCrumbBar.BreadCrumbs.Count > 1;
    public void GoBack() => BreadCrumbBar.BreadCrumbs.RemoveAt(BreadCrumbBar.BreadCrumbs.Count - 1);

    private void UpdateConnectionStatus()
    {
        IsMalConnected = _trackingServiceContext.IsTrackerAuthenticated(ListServiceType.MyAnimeList);
        IsAniListConnected = _trackingServiceContext.IsTrackerAuthenticated(ListServiceType.AniList);
        IsSimklConnected = _trackingServiceContext.IsTrackerAuthenticated(ListServiceType.Simkl);
    }

    public static string ElementThemeToString(ElementTheme theme) => theme.ToString();
    public string GetDescripton() => $"Client - {Version}";

}


public class Dto<T> : ReactiveObject
{
    public Dto(T model, Key<T> key, ILocalSettingsService localSettingService)
    {
        Changed
            .Select(x => GetType().GetProperty(x.PropertyName))
            .Subscribe(propInfo =>
            {
                var value = propInfo.GetValue(this);
                typeof(T).GetProperty(propInfo.Name).SetValue(model, value);
                this.Log().Debug($"""Setting Changed "{key.Name}.{propInfo.Name}" => {value}""");
                localSettingService.SaveSetting(key, model);
            });
    }

}