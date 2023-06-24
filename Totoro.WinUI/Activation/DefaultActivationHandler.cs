using Microsoft.UI.Xaml;
using Totoro.Core.ViewModels;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.MediaDetection;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.ViewModels;

namespace Totoro.WinUI.Activation;

public class DefaultActivationHandler : ActivationHandler<LaunchActivatedEventArgs>
{
    private readonly IWinUINavigationService _navigationService;
    private readonly ITrackingServiceContext _trackingService;
    private readonly ISettings _settings;
    private readonly IViewService _viewService;
    private readonly ProcessWatcher _processWatcher;

    public DefaultActivationHandler(IWinUINavigationService navigationService,
                                    ITrackingServiceContext trackingService,
                                    ISettings settings,
                                    IViewService viewService,
                                    IDiscordRichPresense drpc,
                                    ProcessWatcher processWatcher)
    {
        _navigationService = navigationService;
        _trackingService = trackingService;
        _settings = settings;
        _viewService = viewService;
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
    }

    protected override bool CanHandleInternal(LaunchActivatedEventArgs args)
    {
        // None of the ActivationHandlers has handled the activation.
        return _navigationService.Frame.Content == null;
    }

    protected override Task HandleInternalAsync(LaunchActivatedEventArgs args)
    {
        if (_trackingService.IsAuthenticated)
        {
            NavigateToHome();
        }
        else
        {
            _navigationService.NavigateTo<DiscoverViewModel>();
        }

        if (!PluginFactory<AnimeProvider>.Instance.Plugins.Any())
        {
            _viewService.Information("Ops...", "There has been a breaking change, please wait till application downloads update");
        }

        _processWatcher.Start();

        return Task.CompletedTask;
    }

    private void NavigateToHome()
    {
        if (_settings.HomePage == "Discover")
        {
            _navigationService.NavigateTo<DiscoverViewModel>();
        }
        else if (_settings.HomePage == "My List")
        {
            _navigationService.NavigateTo<UserListViewModel>();
        }
    }
}
