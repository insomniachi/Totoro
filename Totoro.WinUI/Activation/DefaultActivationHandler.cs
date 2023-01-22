using Microsoft.UI.Xaml;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Contracts;

namespace Totoro.WinUI.Activation;

public class DefaultActivationHandler : ActivationHandler<LaunchActivatedEventArgs>
{
    private readonly IWinUINavigationService _navigationService;
    private readonly ITrackingServiceContext _trackingService;
    private readonly ISettings _settings;

    public DefaultActivationHandler(IWinUINavigationService navigationService,
                                    ITrackingServiceContext trackingService,
                                    ISettings settings)
    {
        _navigationService = navigationService;
        _trackingService = trackingService;
        _settings = settings;
    }

    protected override bool CanHandleInternal(LaunchActivatedEventArgs args)
    {
        // None of the ActivationHandlers has handled the activation.
        return _navigationService.Frame.Content == null;
    }

    protected override Task HandleInternalAsync(LaunchActivatedEventArgs args)
    {
        if(_trackingService.IsAuthenticated)
        {
            NavigateToHome();
        }
        else
        {
            _navigationService.NavigateTo<DiscoverViewModel>();
        }

        return Task.CompletedTask;
    }

    private void NavigateToHome()
    {
        if(_settings.HomePage == "Discover")
        {
            _navigationService.NavigateTo<DiscoverViewModel>();
        }
        else if(_settings.HomePage == "My List")
        {
            _navigationService.NavigateTo<UserListViewModel>();
        }
    }
}
