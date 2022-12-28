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
    public List<ElementTheme> Themes { get; } = Enum.GetValues<ElementTheme>().Cast<ElementTheme>().ToList();
    public List<ProviderType> ProviderTypes { get; } = new List<ProviderType> { ProviderType.AllAnime, ProviderType.AnimePahe, ProviderType.GogoAnime, ProviderType.Yugen };
    public List<LogLevel> LogLevels { get; } = new List<LogLevel> { LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error, LogLevel.Critical };
    public ICommand AuthenticateCommand { get; }

    public SettingsViewModel(IThemeSelectorService themeSelectorService,
                             ILocalSettingsService localSettingsService,
                             IViewService viewService,
                             IDiscordRichPresense dRpc)
    {
        ElementTheme = themeSelectorService.Theme;
        PreferSubs = localSettingsService.ReadSetting(nameof(PreferSubs), true);
        DefaultProviderType = localSettingsService.ReadSetting(nameof(DefaultProviderType), ProviderType.GogoAnime);
        UseDiscordRichPresense = localSettingsService.ReadSetting(nameof(UseDiscordRichPresense), false);
        TimeRemainingWhenEpisodeCompletesInSeconds = localSettingsService.ReadSetting(nameof(TimeRemainingWhenEpisodeCompletesInSeconds), 120);
        OpeningSkipDurationInSeconds = localSettingsService.ReadSetting(nameof(OpeningSkipDurationInSeconds), 85);
        ContributeTimeStamps = localSettingsService.ReadSetting(nameof(ContributeTimeStamps), false);
        DefaultUrls = localSettingsService.ReadSetting(nameof(DefaultUrls), new DefaultUrls());
        MinimumLogLevel = localSettingsService.ReadSetting(nameof(MinimumLogLevel), LogLevel.Debug);

        var id = localSettingsService.ReadSetting(nameof(AniSkipId), Guid.Empty);
        if (id == Guid.Empty)
        {
            AniSkipId = Guid.NewGuid();
            localSettingsService.SaveSetting(nameof(AniSkipId), AniSkipId);
        }

        AuthenticateCommand = ReactiveCommand.CreateFromTask(viewService.AuthenticateMal);

        if (UseDiscordRichPresense && !dRpc.IsInitialized)
        {
            dRpc.Initialize();
            dRpc.SetPresence();
        }

        Changed
            .Select(x => GetType().GetProperty(x.PropertyName))
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

        this.WhenAnyValue(x => x.DefaultUrls)
            .SelectMany(UpdateUrls)
            .Subscribe();

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

    private async Task<Unit> UpdateUrls(DefaultUrls urls)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri(string.IsNullOrEmpty(urls.GogoAnime) ? AnimDL.Core.DefaultUrl.GogoAnime : urls.GogoAnime));
        if (response.StatusCode == System.Net.HttpStatusCode.MovedPermanently)
        {
            urls.GogoAnime = response.Headers.Location.AbsoluteUri;
        }

        AnimDL.Core.DefaultUrl.GogoAnime = urls.GogoAnime;
        return Unit.Default;
    }

    public static string ElementThemeToString(ElementTheme theme) => theme.ToString();

}
