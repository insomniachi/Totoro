using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Splat;
using Totoro.Core.Services;
using Totoro.Plugins.MediaDetection.Contracts;
using Totoro.WinUI.Activation;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Services;

public class ActivationService : IActivationService, IEnableLogger
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IInitializer _initializer;
    private readonly IPluginOptionsStorage<INativeMediaPlayer> _mediaPlayerPluginOptions;
    private readonly ISettings _settings;
    private readonly string _prevWebviewFolder;
    private readonly string _tempPath = Path.GetTempPath();
    private readonly WindowPersistenceService _windowPersistenceService;

    public bool IsAuthenticated { get; set; } = true;

    public ActivationService(ActivationHandler<LaunchActivatedEventArgs> defaultHandler,
                             IEnumerable<IActivationHandler> activationHandlers,
                             IThemeSelectorService themeSelectorService,
                             IInitializer initializer,
                             IPluginOptionsStorage<INativeMediaPlayer> mediaPlayerPluginOptions,
                             ISettings settings,
                             ILocalSettingsService localSettingsService,
                             IWindowService windowService)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _initializer = initializer;
        _mediaPlayerPluginOptions = mediaPlayerPluginOptions;
        _settings = settings;
        _prevWebviewFolder = Environment.GetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER");
        _windowPersistenceService = new WindowPersistenceService(localSettingsService, windowService, App.MainWindow, "MainWindow");

        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", _tempPath);
    }

    public async Task ActivateAsync(object activationArgs)
    {
        // Execute tasks before activation.
        await InitializeAsync();

        // Handle activation via ActivationHandlers.
        await HandleActivationAsync(activationArgs);

        // Activate the MainWindow.
        if (!_settings.StartupOptions.StartMinimizedToTray)
        {
            App.MainWindow.Activate();
        }

        App.MainWindow.AppWindow.Closing += AppWindow_Closing;

        // Execute tasks after activation.
        await StartupAsync();
    }

    private async void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        this.Log().Info("Closing MainWindow");
        if (!string.IsNullOrEmpty(_prevWebviewFolder) && _prevWebviewFolder != _tempPath)
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", _prevWebviewFolder);
        }

        await _initializer.ShutDown();
        NativeMethods.AllowSleep();
        App.MainWindow.Close();
    }

    private async Task HandleActivationAsync(object activationArgs)
    {
        var activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));

        if (activationHandler != null)
        {
            await activationHandler.HandleAsync(activationArgs);
        }

        if (_defaultHandler.CanHandle(activationArgs))
        {
            await _defaultHandler.HandleAsync(activationArgs);
        }
    }

    private async Task InitializeAsync()
    {
        _themeSelectorService.Initialize();
        await _initializer.Initialize();
        _mediaPlayerPluginOptions.Initialize();
    }

    private Task StartupAsync()
    {
        _themeSelectorService.SetRequestedTheme();
        return Task.CompletedTask;
    }
}
