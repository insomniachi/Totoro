using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Nodes;
using AnimDL.WinUI.Activation;
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

    private async Task InitializeAsync()
    {
        _themeSelectorService.Initialize();
        await Task.CompletedTask;
    }

    private async Task StartupAsync()
    {
        _themeSelectorService.SetRequestedTheme();

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.102 Safari/537.36 Edg/104.0.1293.70");
            var response = await client.GetStringAsync("https://api.github.com/repos/athulrajts/AnimDL.GUI/releases/latest");
            if(!string.IsNullOrEmpty(response))
            {
                var jObject = JsonNode.Parse(response);
                var latestVersion = new Version(jObject["tag_name"].ToString());
                var appVersion = Assembly.GetExecutingAssembly().GetName().Version;

                if(latestVersion > appVersion)
                {
                    var dialog = new ContentDialog()
                    {
                        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                        XamlRoot = App.MainWindow.Content.XamlRoot,
                        DefaultButton = ContentDialogButton.Primary,
                        Content = $"A new version {latestVersion} is available for download",
                        PrimaryButtonText = "Download now",
                        CloseButtonText = "Maybe later",
                        PrimaryButtonCommand = ReactiveCommand.Create(() =>
                        {
                            Process.Start(new ProcessStartInfo(jObject["html_url"].ToString())
                            {
                                UseShellExecute = true
                            });
                        })
                    };

                    await dialog.ShowAsync();
                }
            }
        }
        catch { }
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
