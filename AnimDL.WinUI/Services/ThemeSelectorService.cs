using AnimDL.WinUI.Contracts;

using Microsoft.UI.Xaml;

namespace AnimDL.WinUI.Services;

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
        if (App.MainWindow.Content is not FrameworkElement rootElement)
        {
            return;
        }

        rootElement.RequestedTheme = Theme;
        TitleBarHelper.UpdateTitleBar(Theme);
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

    private void SaveThemeInSettingsAsync(ElementTheme theme)
    {
        _localSettingsService.SaveSetting(_settingsKey, theme.ToString());
    }
}
