using AnimDL.Core.Api;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Contracts;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Services;

public class ViewService : IViewService
{
    private readonly IContentDialogService _contentDialogService;

    public ViewService(IContentDialogService contentDialogService)
    {
        _contentDialogService = contentDialogService;
    }

    public async Task<Unit> UpdateTracking(IAnimeModel a)
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
            vm.UpdateTracking();
        }

        return Unit.Default;
    }

    public async Task<SearchResult> ChoooseSearchResult(SearchResult closestMatch, List<SearchResult> searchResults, string providerType)
    {
        var vm = App.GetService<ChooseSearchResultViewModel>();
        vm.SetValues(searchResults);
        vm.SelectedSearchResult = closestMatch;
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

    public Task Authenticate(ListServiceType type)
    {
        if (type == ListServiceType.AniList)
        {
            return _contentDialogService.ShowDialog<AuthenticateAniListViewModel>(d =>
            {
                d.Title = "Authenticate";
                d.IsPrimaryButtonEnabled = false;
                d.IsSecondaryButtonEnabled = false;
                d.CloseButtonText = "Ok";
                d.Width = App.MainWindow.Bounds.Width;
            });
        }
        else
        {
            return _contentDialogService.ShowDialog<AuthenticateMyAnimeListViewModel>(d =>
            {
                d.Title = "Authenticate";
                d.IsPrimaryButtonEnabled = false;
                d.IsSecondaryButtonEnabled = false;
                d.CloseButtonText = "Ok";
                d.Width = App.MainWindow.Bounds.Width;
            });
        }
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
        where T : class
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

    public async Task SubmitTimeStamp(long malId, int ep, VideoStream stream, AniSkipResult existingResult, double duration, double introStart)
    {
        var vm = new SubmitTimeStampsViewModel(App.GetService<ITimestampsService>()) // TODO fix later
        {
            Stream = stream,
            MalId = malId,
            Episode = ep,
            StartPosition = introStart,
            SuggestedStartPosition = introStart,
            EndPosition = introStart + 85,
            Duration = duration,
            ExistingResult = existingResult
        };

        await _contentDialogService.ShowDialog(vm, d =>
        {
            d.Title = "Submit Timestamp";
            d.IsPrimaryButtonEnabled = true;
            d.IsSecondaryButtonEnabled = false;
            d.PrimaryButtonText = "Close";
        });

        vm.MediaPlayer.Pause();
    }

    public async Task<bool> Question(string title, string message)
    {
        var dialog = new ContentDialog()
        {
            Title = title,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            DefaultButton = ContentDialogButton.Primary,
            Content = message,
            PrimaryButtonText = "Yes",
            SecondaryButtonText = "No"
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task<Unit> Information(string title, string message)
    {
        var dialog = new ContentDialog()
        {
            Title = title,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            DefaultButton = ContentDialogButton.Primary,
            Content = new MarkdownTextBlock() { Text = message, TextWrapping = TextWrapping.WrapWholeWords, Padding = new Thickness(10)},
            PrimaryButtonText = "Yes",
        };

        var result = await dialog.ShowAsync();
        return Unit.Default;
    }

    public async Task<Unit> ConfigureProvider(ProviderInfo provider)
    {
        var vm = new ConfigureProviderViewModel(App.GetService<IPluginManager>())
        {
            ProviderType = provider.Name
        };

        var result = await _contentDialogService.ShowDialog(vm, d =>
        {
            d.Title = provider.DisplayName;
            d.IsPrimaryButtonEnabled = true;
            d.IsSecondaryButtonEnabled = true;
            d.PrimaryButtonText = "Save";
            d.SecondaryButtonText = "Cancel";
            d.PrimaryButtonCommand = vm.Save;
        });

        return Unit.Default;
    }
}
