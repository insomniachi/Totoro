using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Services;

public class ThemeSelectorService : IThemeSelectorService
{
    private const string _settingsKey = "AppBackgroundRequestedTheme";

    public ElementTheme Theme { get; set; } = ElementTheme.Dark;

    private readonly ILocalSettingsService _localSettingsService;

    public ThemeSelectorService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public void Initialize()
    {
        Theme = LoadThemeFromSettings();
    }

    public void SetTheme(ElementTheme theme)
    {
        Theme = theme;
        SetRequestedTheme();
        SaveThemeInSettings(Theme);
    }

    public void SetRequestedTheme()
    {
        if (App.MainWindow.WindowContent is not Microsoft.UI.Xaml.FrameworkElement rootElement)
        {
            return;
        }

        var convertedTheme = Convert(Theme);

        if (rootElement.RequestedTheme == convertedTheme)
        {
            return;
        }

        rootElement.RequestedTheme = convertedTheme;
        TitleBarHelper.UpdateTitleBar(rootElement.RequestedTheme);
    }

    private ElementTheme LoadThemeFromSettings()
    {
        return _localSettingsService.ReadSetting(_settingsKey, ElementTheme.Dark);
    }

    private static Microsoft.UI.Xaml.ElementTheme Convert(ElementTheme theme) => theme switch
    {
        ElementTheme.Default => Microsoft.UI.Xaml.ElementTheme.Default,
        ElementTheme.Dark => Microsoft.UI.Xaml.ElementTheme.Dark,
        ElementTheme.Light => Microsoft.UI.Xaml.ElementTheme.Light,
        _ => throw new ArgumentException("invalid value", nameof(theme))
    };

    private void SaveThemeInSettings(ElementTheme theme)
    {
        _localSettingsService.SaveSetting(_settingsKey, theme);
    }
}
