using System.Collections.Generic;
using System.Threading.Tasks;
using AnimDL.Api;
using AnimDL.Core.Models;
using AnimDL.WinUI.Contracts.Services;
using AnimDL.WinUI.Dialogs.ViewModels;
using MalApi;
using MalApi.Interfaces;
using Microsoft.UI.Xaml.Controls;

namespace AnimDL.WinUI.Services;

public class ViewService : IViewService
{
    private readonly IContentDialogService _contentDialogService;
    private readonly IMalClient _client;

    public ViewService(IContentDialogService contentDialogService,
                       IMalClient client)
    {
        _contentDialogService = contentDialogService;
        _client = client;
    }

    public async Task UpdateAnimeStatus(Anime a)
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
            var request = _client.Anime()
                                 .WithId(a.Id)
                                 .UpdateStatus()
                                 .WithStatus(vm.Status);

            if(vm.Score is { } s)
            {
                request.WithScore(s);
            }
            if(vm.EpisodesWatched > 0)
            {
                request.WithEpisodesWatched((int)vm.EpisodesWatched);
            }

            a.UserStatus = await request.Publish();
        }
    }

    public async Task<SearchResult> ChoooseSearchResult(List<SearchResult> searchResults, ProviderType providerType)
    {
        var vm = App.GetService<ChooseSearchResultViewModel>();
        vm.SetValues(searchResults);
        vm.SelectedSearchResult = searchResults[0];
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
