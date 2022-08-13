using System;
using System.Reactive.Linq;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Helpers;
using Microsoft.UI.Xaml.Navigation;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AnimDL.WinUI.ViewModels;

public partial class ShellViewModel : ReactiveObject
{
    [Reactive] public object Selected { get; set; }
    [Reactive] public bool IsBackEnabled { get; set; }
    [Reactive] public bool IsAuthenticated { get; set; }

    public INavigationService NavigationService { get; }
    public INavigationViewService NavigationViewService { get; set; }

    public ShellViewModel(INavigationService navigationService,
                          INavigationViewService navigationViewService,
                          IMessageBus messageBus)
    {
        NavigationService = navigationService;
        NavigationService.Navigated.Subscribe(OnNavigated);
        NavigationViewService = navigationViewService;

        messageBus.Listen<MalAuthenticatedMessage>()
                  .ObserveOn(RxApp.MainThreadScheduler)
                  .Subscribe(_ => IsAuthenticated = true);
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
