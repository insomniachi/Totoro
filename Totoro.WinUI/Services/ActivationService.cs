using MalApi.Interfaces;
using Microsoft.UI.Xaml;
using Totoro.WinUI.Activation;

namespace Totoro.WinUI.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IPlaybackStateStorage _playbackStateStorage;
    private readonly string _prevWebviewFolder;
    private readonly string _tempPath = System.IO.Path.GetTempPath();

    public bool IsAuthenticated { get; set; } = true;

    public ActivationService(ActivationHandler<LaunchActivatedEventArgs> defaultHandler,
                             IEnumerable<IActivationHandler> activationHandlers,
                             IThemeSelectorService themeSelectorService,
                             IPlaybackStateStorage playbackStateStorage,
                             IMalClient malClient)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _playbackStateStorage = playbackStateStorage;
        IsAuthenticated = malClient.IsAuthenticated;
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

        App.MainWindow.Closed += MainWindow_Closed;

        // Execute tasks after activation.
        await StartupAsync();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (!string.IsNullOrEmpty(_prevWebviewFolder) && _prevWebviewFolder != _tempPath)
        {
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", _prevWebviewFolder);
        }
        _playbackStateStorage.StoreState();
        App.MainWindow.Closed -= MainWindow_Closed;
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

    private Task InitializeAsync()
    {
        _themeSelectorService.Initialize();
        return Task.CompletedTask;
    }

    private Task StartupAsync()
    {
        _themeSelectorService.SetRequestedTheme();
        return Task.CompletedTask;
    }
}
