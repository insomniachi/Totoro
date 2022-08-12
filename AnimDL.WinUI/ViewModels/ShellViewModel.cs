using AnimDL.WinUI.Contracts.Services;
using AnimDL.WinUI.Helpers;
using AnimDL.WinUI.Views;
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
                          INavigationViewService navigationViewService)
    {
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
        NavigationViewService = navigationViewService;
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
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
