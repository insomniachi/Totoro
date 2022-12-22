using AnimDL.Api;

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
    public List<ElementTheme> Themes { get; set; } = Enum.GetValues<ElementTheme>().Cast<ElementTheme>().ToList();
    public List<ProviderType> ProviderTypes { get; set; } = Enum.GetValues<ProviderType>().Cast<ProviderType>().ToList();
    public ICommand AuthenticateCommand { get; }

    public SettingsViewModel(IThemeSelectorService themeSelectorService,
                             ILocalSettingsService localSettingsService,
                             IViewService viewService,
                             IDiscordRichPresense dRpc)
    {
        ElementTheme = themeSelectorService.Theme;
        PreferSubs = localSettingsService.ReadSetting(nameof(PreferSubs), true);
        DefaultProviderType = localSettingsService.ReadSetting(nameof(DefaultProviderType), ProviderType.AnimixPlay);
        UseDiscordRichPresense = localSettingsService.ReadSetting(nameof(UseDiscordRichPresense), false);
        TimeRemainingWhenEpisodeCompletesInSeconds = localSettingsService.ReadSetting(nameof(TimeRemainingWhenEpisodeCompletesInSeconds), 120);
        OpeningSkipDurationInSeconds = localSettingsService.ReadSetting(nameof(OpeningSkipDurationInSeconds), 85);
        ContributeTimeStamps = localSettingsService.ReadSetting(nameof(ContributeTimeStamps), false);

        var id = localSettingsService.ReadSetting(nameof(AniSkipId), Guid.Empty);
        if(id == Guid.Empty)
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
                localSettingsService.SaveSetting(propInfo.Name, propInfo.GetValue(this));
            });

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

    public static string ElementThemeToString(ElementTheme theme) => theme.ToString();

}
