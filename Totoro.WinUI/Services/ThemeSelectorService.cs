using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Services;

public class ThemeSelectorService : IThemeSelectorService
{
    private const string _settingsKey = "AppBackgroundRequestedTheme";

    public ElementTheme Theme { get; set; } = ElementTheme.Default;

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
        SaveThemeInSettingsAsync(Theme);
    }

    public void SetRequestedTheme()
    {
        if (App.MainWindow.Content is not Microsoft.UI.Xaml.FrameworkElement rootElement)
        {
            return;
        }

        rootElement.RequestedTheme = Convert(Theme);
        TitleBarHelper.UpdateTitleBar(rootElement.RequestedTheme);
    }

    private ElementTheme LoadThemeFromSettings()
    {
        var themeName = _localSettingsService.ReadSetting<string>(_settingsKey);

        if (Enum.TryParse(themeName, out ElementTheme cacheTheme))
        {
            return cacheTheme;
        }

        return ElementTheme.Default;
    }

    private static Microsoft.UI.Xaml.ElementTheme Convert(ElementTheme theme) => theme switch
    {
        ElementTheme.Default => Microsoft.UI.Xaml.ElementTheme.Default,
        ElementTheme.Dark => Microsoft.UI.Xaml.ElementTheme.Dark,
        ElementTheme.Light => Microsoft.UI.Xaml.ElementTheme.Light,
        _ => throw new ArgumentException("invalid value", nameof(theme))
    };

    private void SaveThemeInSettingsAsync(ElementTheme theme)
    {
        _localSettingsService.SaveSetting(_settingsKey, theme.ToString());
    }
}
