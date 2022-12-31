using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;
using Totoro.Core;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.ViewModels;

public partial class ShellViewModel : ReactiveObject
{
    [Reactive] public object Selected { get; set; }
    [Reactive] public bool IsBackEnabled { get; set; }
    [Reactive] public bool IsAuthenticated { get; set; }

    public IWinUINavigationService NavigationService { get; }
    public INavigationViewService NavigationViewService { get; set; }

    public ShellViewModel(IWinUINavigationService navigationService,
                          INavigationViewService navigationViewService,
                          ITrackingServiceContext trackingService,
                          IUpdateService updateService,
                          IViewService viewService)
    {
        NavigationService = navigationService;
        NavigationService.Navigated.Subscribe(OnNavigated);
        NavigationViewService = navigationViewService;
        IsAuthenticated = trackingService.IsAuthenticated;
        
        MessageBus.Current
            .Listen<MalAuthenticatedMessage>()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => IsAuthenticated = true);

        updateService
            .OnUpdateAvailable
            .SelectMany(updateService.DownloadUpdate)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Select(async vi =>
            {
                var answer = await viewService.Question("Update Downloaded", $"New update version {vi.Version} downloaded, Install now ?");
                if(answer)
                {
                    updateService.InstallUpdate(vi);
                }
            })
            .Subscribe(_ => { }, RxApp.DefaultExceptionHandler.OnError);
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
