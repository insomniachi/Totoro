using AnimDL.Api;
using Microsoft.UI.Xaml.Controls;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Services;

public class ViewService : IViewService
{
    private readonly IContentDialogService _contentDialogService;
    private readonly ITrackingService _trackingService;

    public ViewService(IContentDialogService contentDialogService,
                       ITrackingService trackingService)
    {
        _contentDialogService = contentDialogService;
        _trackingService = trackingService;
    }

    public async Task<Unit> UpdateAnimeStatus(IAnimeModel a)
    {
        var vm = App.GetService<UpdateAnimeStatusViewModel>();
        vm.Anime = a;

        var result = await _contentDialogService.ShowDialog(vm, d =>
        {
            d.Title = a.Title;
            d.PrimaryButtonText = "Update";
            d.IsSecondaryButtonEnabled = false;
            d.CloseButtonText = "Cancel";
        });

        if (result == ContentDialogResult.Primary)
        {
            var tracking = new Tracking();

            if (a.Tracking?.Status != vm.Status)
            {
                tracking.Status = vm.Status;
            }
            if (vm.Score is int score && score != (a.Tracking?.Score ?? 0))
            {
                tracking.Score = score;
            }
            if (vm.EpisodesWatched > 0)
            {
                tracking.WatchedEpisodes = (int)vm.EpisodesWatched;
            }
            if (vm.StartDate is { } sd)
            {
                tracking.StartDate = sd.Date;
            }
            if(vm.FinishDate is { } fd)
            {
                tracking.FinishDate = fd.Date;
            }

            _trackingService.Update(a.Id, tracking).Subscribe(x => a.Tracking = x);
        }

        return Unit.Default;
    }

    public async Task<SearchResult> ChoooseSearchResult(List<SearchResult> searchResults, ProviderType providerType)
    {
        var vm = App.GetService<ChooseSearchResultViewModel>();
        vm.SetValues(searchResults);
        vm.SelectedSearchResult = searchResults.FirstOrDefault();
        vm.SelectedProviderType = providerType;

        await _contentDialogService.ShowDialog(vm, d =>
        {
            d.Title = "Choose title";
            d.IsPrimaryButtonEnabled = false;
            d.IsSecondaryButtonEnabled = false;
            d.CloseButtonText = "Ok";
        });

        vm.Dispose();
        return vm.SelectedSearchResult;
    }

    public async Task AuthenticateMal()
    {
        await _contentDialogService.ShowDialog<AuthenticateMyAnimeListViewModel>(d =>
        {
            d.Title = "Authenticate";
            d.IsPrimaryButtonEnabled = false;
            d.IsSecondaryButtonEnabled = false;
            d.CloseButtonText = "Ok";
            d.Width = App.MainWindow.Bounds.Width;
        });
    }

    public async Task PlayVideo(string title, string url)
    {
        await _contentDialogService.ShowDialog<PlayVideoDialogViewModel>(d =>
        {
            d.Title = title;
            d.IsPrimaryButtonEnabled = false;
            d.IsSecondaryButtonEnabled = false;
            d.CloseButtonText = "Close";
        }, vm => vm.Url = url);
    }

    public async Task<T> SelectModel<T>(IEnumerable<object> models)
        where T: class
    {
        var vm = new SelectModelViewModel()
        {
            Models = models,
        };

        await _contentDialogService.ShowDialog(vm, d =>
        {
            d.Title = "Select";
            d.IsPrimaryButtonEnabled = false;
            d.IsSecondaryButtonEnabled = false;
            d.CloseButtonText = "Ok";
        });

        return vm.SelectedModel as T;
    }
}
