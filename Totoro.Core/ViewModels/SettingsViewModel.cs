using System.Reactive.Concurrency;
using System.Reflection;
using MonoTorrent.Client;
using Splat;
using Totoro.Core.Torrents;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Torrents.Contracts;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Totoro.Core.ViewModels;

public class SettingsViewModel : NavigatableViewModel
{
    private readonly ITrackingServiceContext _trackingServiceContext;

    [Reactive] public bool IsMalConnected { get; set; }
    [Reactive] public bool IsAniListConnected { get; set; }
    [Reactive] public PluginInfo SelectedProvider { get; set; }
    [Reactive] public string NyaaUrl { get; set; }
    [Reactive] public ElementTheme Theme { get; set; }
    [ObservableAsProperty] public bool HasInactiveTorrents { get; }
    public ObservableCollection<TorrentManager> InactiveTorrents { get; }

    public ISettings Settings { get; }
    public Version Version { get; }
    public Version ScrapperVersion { get; }
    public List<ElementTheme> Themes { get; } = Enum.GetValues<ElementTheme>().Cast<ElementTheme>().ToList();
    public IEnumerable<PluginInfo> ProviderTypes => PluginFactory<AnimeProvider>.Instance.Plugins;
    public IEnumerable<PluginInfo> TrackerTypes => PluginFactory<ITorrentTracker>.Instance.Plugins;
    public List<LogLevel> LogLevels { get; } = new List<LogLevel> { LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical };
    public List<ListServiceType> ServiceTypes { get; } = new List<ListServiceType> { ListServiceType.MyAnimeList, ListServiceType.AniList };
    public List<string> HomePages { get; } = new List<string> { "Discover", "My List" };
    public List<string> AnimeActions { get; } = new List<string> { "Watch", "Info" };
    public List<StreamQualitySelection> QualitySelections { get; } = Enum.GetValues<StreamQualitySelection>().Cast<StreamQualitySelection>().ToList();
    public List<DebridServiceType> DebridServices { get; } = Enum.GetValues<DebridServiceType>().Cast<DebridServiceType>().ToList();
    public List<TorrentProviderType> TorrentProviderTypes { get; } = Enum.GetValues<TorrentProviderType>().Cast<TorrentProviderType>().ToList();
    public List<MediaPlayerType> MediaPlayerTypes { get; } = Enum.GetValues<MediaPlayerType>().Cast<MediaPlayerType>().ToList();
    public ICommand AuthenticateCommand { get; }
    public ICommand ShowAbout { get; }
    public ICommand ConfigureProvider { get; }
    public ICommand Navigate { get; }
    public ICommand EditUserTorrentDirectory { get; }

    public BreadCrumbBarModel BreadCrumbBar { get; } = new("Settings");

    public SettingsViewModel(ISettings settings,
                             ITrackingServiceContext trackingServiceContext,
                             IViewService viewService,
                             IUpdateService updateService,
                             ILocalSettingsService localSettingsService,
                             IThemeSelectorService themeSelectorService,
                             ITorrentEngine torrentEngine)
    {
        _trackingServiceContext = trackingServiceContext;

        Settings = settings;
        Version = Assembly.GetEntryAssembly().GetName().Version;
        SelectedProvider = PluginFactory<AnimeProvider>.Instance.Plugins.FirstOrDefault(x => x.Name == settings.DefaultProviderType)
            ?? PluginFactory<AnimeProvider>.Instance.Plugins.FirstOrDefault(x => x.Name == "allanime");
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
        ConfigureProvider = ReactiveCommand.CreateFromTask(() => viewService.ConfigureProvider(SelectedProvider));
        Navigate = ReactiveCommand.Create<string>(BreadCrumbBar.BreadCrumbs.Add);
        EditUserTorrentDirectory = ReactiveCommand.Create(async () =>
        {
            var folder = await viewService.BrowseFolder();
            if(string.IsNullOrEmpty(folder))
            {
                return;
            }

            RxApp.MainThreadScheduler.Schedule(() => Settings.UserTorrentsDownloadDirectory = folder);
        });

        InactiveTorrents = new(torrentEngine.TorrentManagers.Where(x => x.State == MonoTorrent.Client.TorrentState.Stopped));
        Theme = themeSelectorService.Theme;

        NyaaUrl = localSettingsService.ReadSetting("Nyaa", "https://nyaa.ink/");

        this.ObservableForProperty(x => x.SelectedProvider, x => x)
            .Subscribe(provider => settings.DefaultProviderType = provider.Name);

        this.ObservableForProperty(x => x.NyaaUrl, x => x)
            .Where(x => !string.IsNullOrEmpty(x))
            .Subscribe(x => localSettingsService.SaveSetting("Nyaa", x));

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

    private void UpdateConnectionStatus()
    {
        IsMalConnected = _trackingServiceContext.IsTrackerAuthenticated(ListServiceType.MyAnimeList);
        IsAniListConnected = _trackingServiceContext.IsTrackerAuthenticated(ListServiceType.AniList);
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