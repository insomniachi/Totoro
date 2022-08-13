using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AnimDL.WinUI.Activation;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Views;
using MalApi.Interfaces;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace AnimDL.WinUI.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IPlaybackStateStorage _playbackStateStorage;
    private UIElement _shell = null;
    private readonly string prevWebviewFolder;
    private readonly string tempPath = System.IO.Path.GetTempPath();

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
        prevWebviewFolder = System.Environment.GetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER");
        System.Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", tempPath);
    }

    public async Task ActivateAsync(object activationArgs)
    {
        // Execute tasks before activation.
        await InitializeAsync();

        // Set the MainWindow Content.
        if (App.MainWindow.Content == null)
        {
            var shell = App.GetService<ShellPage>();
            _shell = shell;
            shell.ViewModel.IsAuthenticated = IsAuthenticated;
            App.MainWindow.Content = _shell ?? new Frame();
        }

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
        if (!string.IsNullOrEmpty(prevWebviewFolder) && prevWebviewFolder != tempPath)
        {
            System.Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", prevWebviewFolder);
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

    private async Task InitializeAsync()
    {
        _themeSelectorService.Initialize();
        await Task.CompletedTask;
    }

    private async Task StartupAsync()
    {
        _themeSelectorService.SetRequestedTheme();
        await RequestFullscreen();
    }

    private static async Task RequestFullscreen()
    {
        var windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        await Task.Delay(250);
        appWindow.SetPresenter(AppWindowPresenterKind.Default);
        appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
    }
}
