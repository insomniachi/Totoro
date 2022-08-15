using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AnimDL.Api;
using AnimDL.Core.Models;
using AnimDL.UI.Core.Contracts;
using AnimDL.UI.Core.Models;
using AnimDL.WinUI.Contracts;
using AnimDL.WinUI.Dialogs.ViewModels;
using MalApi.Interfaces;
using Microsoft.UI.Xaml.Controls;

namespace AnimDL.WinUI.Services;

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

    public async Task UpdateAnimeStatus(AnimeModel a)
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

        if(result == ContentDialogResult.Primary)
        {
            var tracking = new Tracking() { Status = vm.Status };

            if(vm.Score is { } s)
            {
                tracking.Score = s;
            }
            if(vm.EpisodesWatched > 0)
            {
                tracking.WatchedEpisodes = (int)vm.EpisodesWatched;
            }

            _trackingService.Update(a.Id, tracking).Subscribe(x => a.Tracking = x);
        }
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
}
