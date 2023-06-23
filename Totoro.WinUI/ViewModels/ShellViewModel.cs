using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.ViewModels;
using Totoro.Plugins.MediaDetection;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.ViewModels;

public partial class ShellViewModel : ReactiveObject
{
    private readonly ProcessWatcher _processWatcher;

    [Reactive] public object Selected { get; set; }
    [Reactive] public bool IsBackEnabled { get; set; }
    [Reactive] public bool IsAuthenticated { get; set; }
    [ObservableAsProperty] public bool IsWatchView { get; }
    [ObservableAsProperty] public bool IsAboutView { get; }

    public IWinUINavigationService NavigationService { get; }
    public INavigationViewService NavigationViewService { get; set; }

    public ShellViewModel(IWinUINavigationService navigationService,
                          INavigationViewService navigationViewService,
                          ITrackingServiceContext trackingService,
                          IUpdateService updateService,
                          IViewService viewService,
                          IDiscordRichPresense drpc,
                          ProcessWatcher processWatcher)
    {
        NavigationService = navigationService;
        NavigationService.Navigated.Subscribe(OnNavigated);
        NavigationViewService = navigationViewService;
        IsAuthenticated = trackingService.IsAuthenticated;
        _processWatcher = processWatcher;

        processWatcher
            .MediaPlayerDetected
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(player =>
            {
                navigationService.NavigateTo<NowPlayingViewModel>(parameter: new Dictionary<string, object>
                {
                    ["Player"] = player
                });
            });

        processWatcher
            .MediaPlayerClosed
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                drpc.Clear();
                navigationService.GoBack();
            });

        trackingService
            .Authenticated
            .Subscribe(_ => IsAuthenticated = trackingService.IsAuthenticated);

        updateService
            .OnUpdateAvailable
            .SelectMany(updateService.DownloadUpdate)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(async vi =>
            {
                var answer = await viewService.Question("Update Downloaded", $"New update version {vi.Version} downloaded, Install now ?");
                if (answer)
                {
                    updateService.InstallUpdate(vi);
                }
            })
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.Selected)
            .Select(x => x is NavigationViewItem { Content: "Watch" })
            .ToPropertyEx(this, x => x.IsWatchView);

        this.WhenAnyValue(x => x.Selected)
            .Select(x => x is NavigationViewItem { Content: "About" })
            .ToPropertyEx(this, x => x.IsAboutView);
    }

    private void OnNavigated(NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;
        var vmType = NavigationService.Frame.GetPageViewModel().GetType();

        if (vmType == typeof(SettingsViewModel))
        {
            Selected = NavigationViewService.SettingsItem;
            return;
        }

        var selectedItem = NavigationViewService.GetSelectedItem(vmType);
        if (selectedItem != null)
        {
            Selected = selectedItem;
        }
    }
}
