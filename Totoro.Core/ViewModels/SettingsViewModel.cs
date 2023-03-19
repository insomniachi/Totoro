using System.Reflection;
using System.Text.Json.Serialization;
using AnimDL.Core;
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
        PreferSubs = localSettingsService.ReadSetting(nameof(PreferSubs), true);

        // temp hack for backward compat
        try
        {
            DefaultProviderType = localSettingsService.ReadSetting(nameof(DefaultProviderType), "animepahe");
        }
        catch
        {
            DefaultProviderType = "animepahe";
        }

        UseDiscordRichPresense = localSettingsService.ReadSetting(nameof(UseDiscordRichPresense), false);
        TimeRemainingWhenEpisodeCompletesInSeconds = localSettingsService.ReadSetting(nameof(TimeRemainingWhenEpisodeCompletesInSeconds), 120);
        OpeningSkipDurationInSeconds = localSettingsService.ReadSetting(nameof(OpeningSkipDurationInSeconds), 85);
        ContributeTimeStamps = localSettingsService.ReadSetting(nameof(ContributeTimeStamps), false);
        MinimumLogLevel = localSettingsService.ReadSetting(nameof(MinimumLogLevel), LogLevel.Debug);
        AutoUpdate = localSettingsService.ReadSetting(nameof(AutoUpdate), true);
        DefaultListService = localSettingsService.ReadSetting(nameof(DefaultListService), default(ListServiceType?));
        HomePage = localSettingsService.ReadSetting(nameof(HomePage), "Discover");
        AllowSideLoadingPlugins = localSettingsService.ReadSetting(nameof(AllowSideLoadingPlugins), false);
        DefaultStreamQualitySelection = localSettingsService.ReadSetting(nameof(DefaultStreamQualitySelection), StreamQualitySelection.Auto);
        IncludeNsfw = localSettingsService.ReadSetting(nameof(IncludeNsfw), false);
        EnterFullScreenWhenPlaying = localSettingsService.ReadSetting(nameof(EnterFullScreenWhenPlaying), false);
        DebridServiceType = localSettingsService.ReadSetting(nameof(DebridServiceType), DebridServiceType.Premiumize);
        TorrentProviderType = localSettingsService.ReadSetting(nameof(TorrentProviderType), TorrentProviderType.Nya);

        var id = localSettingsService.ReadSetting(nameof(AniSkipId), Guid.Empty);
        if (id == Guid.Empty)
        {
            AniSkipId = Guid.NewGuid();
            localSettingsService.SaveSetting(nameof(AniSkipId), AniSkipId);
        }

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
    [Reactive] public ListServiceType? DefaultListService { get; set; }
    [Reactive] public Guid AniSkipId { get; set; }
    [Reactive] public string HomePage { get; set; }
    [Reactive] public bool AllowSideLoadingPlugins { get; set; }
    [Reactive] public StreamQualitySelection DefaultStreamQualitySelection { get; set; }
    [Reactive] public bool IncludeNsfw { get; set; }
    [Reactive] public bool EnterFullScreenWhenPlaying { get; set; }
    [Reactive] public DebridServiceType DebridServiceType { get; set; }
    [Reactive] public TorrentProviderType TorrentProviderType { get; set; }

    public async Task<Unit> UpdateUrls()
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri(string.IsNullOrEmpty(DefaultUrls.GogoAnime) ? AnimDL.Core.DefaultUrl.GogoAnime : DefaultUrls.GogoAnime));
        if (response.StatusCode == System.Net.HttpStatusCode.MovedPermanently)
        {
            DefaultUrls.GogoAnime = response.Headers.Location.AbsoluteUri;
        }

        DefaultUrl.GogoAnime = DefaultUrls.GogoAnime;
        return Unit.Default;
    }
}

public class SettingsViewModel : NavigatableViewModel
{
    private readonly ITrackingServiceContext _trackingServiceContext;
    private readonly IDebridServiceOptions _debridOptions;

    [Reactive] public bool IsMalConnected { get; set; }
    [Reactive] public bool IsAniListConnected { get; set; }
    [Reactive] public ProviderInfo SelectedProvider { get; set; }
    [Reactive] public string NyaaUrl { get; set; }

    public ISettings Settings { get; }
    public Version Version { get; }
    public Version ScrapperVersion { get; }
    public List<ElementTheme> Themes { get; } = Enum.GetValues<ElementTheme>().Cast<ElementTheme>().ToList();
    public IEnumerable<ProviderInfo> ProviderTypes => ProviderFactory.Instance.Providers;
    public List<LogLevel> LogLevels { get; } = new List<LogLevel> { LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical };
    public List<ListServiceType> ServiceTypes { get; } = new List<ListServiceType> { ListServiceType.MyAnimeList, ListServiceType.AniList };
    public List<string> HomePages { get; } = new List<string> { "Discover", "My List" };
    public List<StreamQualitySelection> QualitySelections { get; } = Enum.GetValues<StreamQualitySelection>().Cast<StreamQualitySelection>().ToList();
    public List<DebridServiceType> DebridServices { get; } = Enum.GetValues<DebridServiceType>().Cast<DebridServiceType>().ToList();
    public List<TorrentProviderType> TorrentProviderTypes { get; } = Enum.GetValues<TorrentProviderType>().Cast<TorrentProviderType>().ToList();
    public ICommand AuthenticateCommand { get; }
    public ICommand ShowAbout { get; }
    public ICommand ConfigureProvider { get; }
    public ICommand ConfigureDebridService { get; }

    public SettingsViewModel(ISettings settings,
                             ITrackingServiceContext trackingServiceContext,
                             IViewService viewService,
                             IUpdateService updateService,
                             ILocalSettingsService localSettingsService,
                             IDebridServiceOptions debridOptions)
    {
        _trackingServiceContext = trackingServiceContext;
        _debridOptions = debridOptions;

        Settings = settings;
        Version = Assembly.GetEntryAssembly().GetName().Version;
        SelectedProvider = ProviderFactory.Instance.Providers.FirstOrDefault(x => x.Name == settings.DefaultProviderType)
            ?? ProviderFactory.Instance.Providers.FirstOrDefault(x => x.Name == "allanime");
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
        ConfigureDebridService = ReactiveCommand.CreateFromTask(() => viewService.ConfigureOptions(settings.DebridServiceType, t => debridOptions[t], (t, _) => debridOptions.Save(t)));
        
        NyaaUrl = localSettingsService.ReadSetting("Nyaa", "https://nyaa.ink/");

        this.ObservableForProperty(x => x.SelectedProvider, x => x)
            .Subscribe(provider => settings.DefaultProviderType = provider.Name);

        this.ObservableForProperty(x => x.NyaaUrl, x => x)
            .Where(x => !string.IsNullOrEmpty(x))
            .Subscribe(x => localSettingsService.SaveSetting("Nyaa", x));

        trackingServiceContext
            .Authenticated
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => UpdateConnectionStatus());

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
