using System.Reflection;
using System.Text.Json.Serialization;
using Splat;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Totoro.Core.ViewModels;

public class SettingsViewModel : NavigatableViewModel, ISettings
{
    [Reactive] public ElementTheme ElementTheme { get; set; }
    [Reactive] public bool PreferSubs { get; set; }
    [Reactive] public ProviderType DefaultProviderType { get; set; }
    [Reactive] public bool IsAuthenticated { get; set; } = true;
    [Reactive] public bool UseDiscordRichPresense { get; set; }
    [Reactive] public int TimeRemainingWhenEpisodeCompletesInSeconds { get; set; }
    [Reactive] public int OpeningSkipDurationInSeconds { get; set; }
    [Reactive] public Guid AniSkipId { get; set; }
    [Reactive] public bool ContributeTimeStamps { get; set; }
    [Reactive] public DefaultUrls DefaultUrls { get; set; }
    [Reactive] public LogLevel MinimumLogLevel { get; set; }
    [Reactive] public bool AutoUpdate { get; set; }
    [Reactive] public ListServiceType? DefaultListService { get; set; }

    [Reactive,JsonIgnore] public bool IsMalConnected { get; set; }
    [Reactive,JsonIgnore] public bool IsAniListConnected { get; set; }

    public Version Version { get; }
    public Version ScrapperVersion { get; }
    public List<ElementTheme> Themes { get; } = Enum.GetValues<ElementTheme>().Cast<ElementTheme>().ToList();
    public List<ProviderType> ProviderTypes { get; } = new List<ProviderType> { ProviderType.AllAnime, ProviderType.AnimePahe, ProviderType.GogoAnime, ProviderType.Yugen, ProviderType.Tenshi };
    public List<LogLevel> LogLevels { get; } = new List<LogLevel> { LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical };
    public List<ListServiceType> ServiceTypes { get; } = new List<ListServiceType> { ListServiceType.MyAnimeList, ListServiceType.AniList };
    public ICommand AuthenticateCommand { get; }
    public ICommand ShowAbout { get; }

    public SettingsViewModel(IThemeSelectorService themeSelectorService,
                             ILocalSettingsService localSettingsService,
                             IViewService viewService,
                             IDiscordRichPresense dRpc,
                             IUpdateService updateService)
    {
        Version = Assembly.GetEntryAssembly().GetName().Version;
        ScrapperVersion = typeof(AnimDL.Core.DefaultUrl).Assembly.GetName().Version;

        if(localSettingsService.ContainsKey("MalToken"))
        {
            IsMalConnected = true;
        }
        if(localSettingsService.ContainsKey("AniListToken"))
        {
            IsAniListConnected = true;
        }


        ElementTheme = themeSelectorService.Theme;
        PreferSubs = localSettingsService.ReadSetting(nameof(PreferSubs), true);
        DefaultProviderType = localSettingsService.ReadSetting(nameof(DefaultProviderType), ProviderType.AllAnime);
        UseDiscordRichPresense = localSettingsService.ReadSetting(nameof(UseDiscordRichPresense), false);
        TimeRemainingWhenEpisodeCompletesInSeconds = localSettingsService.ReadSetting(nameof(TimeRemainingWhenEpisodeCompletesInSeconds), 120);
        OpeningSkipDurationInSeconds = localSettingsService.ReadSetting(nameof(OpeningSkipDurationInSeconds), 85);
        ContributeTimeStamps = localSettingsService.ReadSetting(nameof(ContributeTimeStamps), false);
        DefaultUrls = localSettingsService.ReadSetting(nameof(DefaultUrls), new DefaultUrls());
        MinimumLogLevel = localSettingsService.ReadSetting(nameof(MinimumLogLevel), LogLevel.Debug);
        AutoUpdate = localSettingsService.ReadSetting(nameof(AutoUpdate), true);
        DefaultListService = localSettingsService.ReadSetting(nameof(DefaultListService), default(ListServiceType?));

        var id = localSettingsService.ReadSetting(nameof(AniSkipId), Guid.Empty);
        if (id == Guid.Empty)
        {
            AniSkipId = Guid.NewGuid();
            localSettingsService.SaveSetting(nameof(AniSkipId), AniSkipId);
        }

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
            })
            .DisposeWith(Garbage);

        DefaultUrls
            .WhenAnyPropertyChanged()
            .Subscribe(_ => localSettingsService.SaveSetting(nameof(DefaultUrls), DefaultUrls))
            .DisposeWith(Garbage);

        this.ObservableForProperty(x => x.UseDiscordRichPresense, x => x)
            .Where(x => x && !dRpc.IsInitialized)
            .Subscribe(value =>
            {
                dRpc.Initialize();
                dRpc.SetPresence();
            });
    }

    public override Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters)
    {
        if (parameters.ContainsKey("IsAuthenticated"))
        {
            IsAuthenticated = false;
        }

        return base.OnNavigatedTo(parameters);
    }

    public async Task<Unit> UpdateUrls()
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri(string.IsNullOrEmpty(DefaultUrls.GogoAnime) ? AnimDL.Core.DefaultUrl.GogoAnime : DefaultUrls.GogoAnime));
        if (response.StatusCode == System.Net.HttpStatusCode.MovedPermanently)
        {
            DefaultUrls.GogoAnime = response.Headers.Location.AbsoluteUri;
        }

        AnimDL.Core.DefaultUrl.GogoAnime = DefaultUrls.GogoAnime;
        return Unit.Default;
    }

    public static string ElementThemeToString(ElementTheme theme) => theme.ToString();
    public string GetDescripton() => $"Client - {Version} | Scrapper - {ScrapperVersion}";

}
