using System.Reflection;
using System.Text.Json.Serialization;
using AnimDL.Core;
using Splat;
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
            DefaultProviderType = localSettingsService.ReadSetting(nameof(DefaultProviderType), "allanime");
        }
        catch
        {
            DefaultProviderType = "allanime";
        }

        UseDiscordRichPresense = localSettingsService.ReadSetting(nameof(UseDiscordRichPresense), false);
        TimeRemainingWhenEpisodeCompletesInSeconds = localSettingsService.ReadSetting(nameof(TimeRemainingWhenEpisodeCompletesInSeconds), 120);
        OpeningSkipDurationInSeconds = localSettingsService.ReadSetting(nameof(OpeningSkipDurationInSeconds), 85);
        ContributeTimeStamps = localSettingsService.ReadSetting(nameof(ContributeTimeStamps), false);
        MinimumLogLevel = localSettingsService.ReadSetting(nameof(MinimumLogLevel), LogLevel.Debug);
        AutoUpdate = localSettingsService.ReadSetting(nameof(AutoUpdate), true);
        DefaultListService = localSettingsService.ReadSetting(nameof(DefaultListService), default(ListServiceType?));

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
    [Reactive] public bool IsMalConnected { get; set; }
    [Reactive] public bool IsAniListConnected { get; set; }
    [Reactive] public ProviderInfo SelectedProvider { get; set; }
    
    public ISettings Settings { get; }
    public Version Version { get; }
    public Version ScrapperVersion { get; }
    public List<ElementTheme> Themes { get; } = Enum.GetValues<ElementTheme>().Cast<ElementTheme>().ToList();
    public IEnumerable<ProviderInfo> ProviderTypes => ProviderFactory.Instance.Providers;
    public List<LogLevel> LogLevels { get; } = new List<LogLevel> { LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical };
    public List<ListServiceType> ServiceTypes { get; } = new List<ListServiceType> { ListServiceType.MyAnimeList, ListServiceType.AniList };
    public ICommand AuthenticateCommand { get; }
    public ICommand ShowAbout { get; }

    public SettingsViewModel(ISettings settings,
                             ITrackingServiceContext trackingServiceContext,
                             IViewService viewService,
                             IUpdateService updateService)
    {
        Settings = settings;
        Version = Assembly.GetEntryAssembly().GetName().Version;
        IsMalConnected = trackingServiceContext.IsTrackerAuthenticated(ListServiceType.MyAnimeList);
        IsAniListConnected = trackingServiceContext.IsTrackerAuthenticated(ListServiceType.AniList);
        SelectedProvider = ProviderFactory.Instance.Providers.FirstOrDefault(x => x.Name == settings.DefaultProviderType)
            ?? ProviderFactory.Instance.Providers.FirstOrDefault(x => x.Name == "allanime");

        AuthenticateCommand = ReactiveCommand.CreateFromTask<ListServiceType>(viewService.Authenticate);
        ShowAbout = ReactiveCommand.CreateFromTask(async () =>
        {
            var currentInfo = await updateService.GetCurrentVersionInfo();
            if(currentInfo is null)
            {
                return;
            }    
            await viewService.Information($"{currentInfo.Version}", currentInfo.Body);
        });

        this.ObservableForProperty(x => x.SelectedProvider, x => x)
            .Subscribe(provider => settings.DefaultProviderType = provider.Name);
    }

    public static string ElementThemeToString(ElementTheme theme) => theme.ToString();
    public string GetDescripton() => $"Client - {Version}";

}
