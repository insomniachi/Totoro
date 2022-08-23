using AnimDL.Api;

namespace AnimDL.UI.Core.ViewModels;

public class SettingsViewModel : NavigatableViewModel, ISettings
{
    [Reactive] public ElementTheme ElementTheme { get; set; }
    [Reactive] public bool PreferSubs { get; set; }
    [Reactive] public ProviderType DefaultProviderType { get; set; }
    [Reactive] public bool IsAuthenticated { get; set; } = true;
    [Reactive] public bool UseDiscordRichPresense { get; set; }
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

        AuthenticateCommand = ReactiveCommand.CreateFromTask(viewService.AuthenticateMal);

        if (UseDiscordRichPresense && !dRpc.IsInitialized)
        {
            dRpc.Initialize();
            dRpc.SetPresence();
        }

        this.ObservableForProperty(x => x.ElementTheme, x => x)
            .Subscribe(themeSelectorService.SetTheme);
        this.ObservableForProperty(x => x.PreferSubs, x => x)
            .Subscribe(value => localSettingsService.SaveSetting(nameof(PreferSubs), value));
        this.ObservableForProperty(x => x.DefaultProviderType, x => x)
            .Subscribe(value => localSettingsService.SaveSetting(nameof(DefaultProviderType), value));
        this.ObservableForProperty(x => x.UseDiscordRichPresense, x => x)
            .Subscribe(value =>
            {
                localSettingsService.SaveSetting(nameof(UseDiscordRichPresense), value);
                if (value && !dRpc.IsInitialized)
                {
                    dRpc.Initialize();
                    dRpc.SetPresence();
                }
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
