using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Splat;
using Totoro.WinUI.Activation;
using Totoro.WinUI.Helpers;
using WinUIEx;

namespace Totoro.WinUI.Services;

public class ActivationService : IActivationService, IEnableLogger
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IInitializer _initializer;
    private readonly string _prevWebviewFolder;
    private readonly string _tempPath = Path.GetTempPath();

    public bool IsAuthenticated { get; set; } = true;

    public ActivationService(ActivationHandler<LaunchActivatedEventArgs> defaultHandler,
                             IEnumerable<IActivationHandler> activationHandlers,
                             IThemeSelectorService themeSelectorService,
                             IInitializer initializer)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _initializer = initializer;
        _prevWebviewFolder = Environment.GetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER");

        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", _tempPath);
    }

    public async Task ActivateAsync(object activationArgs)
    {
        // Execute tasks before activation.
        await InitializeAsync();


        // Handle activation via ActivationHandlers.
        await HandleActivationAsync(activationArgs);

        // Activate the MainWindow.
        App.MainWindow.Activate();
        App.MainWindow.Maximize();
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
    }

    private Task StartupAsync()
    {
        _themeSelectorService.SetRequestedTheme();
        return Task.CompletedTask;
    }
}
