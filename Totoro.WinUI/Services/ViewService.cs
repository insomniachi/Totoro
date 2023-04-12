using CommunityToolkit.WinUI.UI.Controls;
using FuzzySharp;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Splat;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Dialogs.ViewModels;
using Totoro.WinUI.Dialogs.Views;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinUIEx;

namespace Totoro.WinUI.Services;

public class ViewService : IViewService, IEnableLogger
{
    private readonly IContentDialogService _contentDialogService;
    private readonly IAnimeServiceContext _animeService;

    public ViewService(IContentDialogService contentDialogService,
                       IAnimeServiceContext animeService)
    {
        _contentDialogService = contentDialogService;
        _animeService = animeService;
    }

    public async Task<Unit> UpdateTracking(IAnimeModel anime)
    {
        var vm = App.GetService<UpdateAnimeStatusViewModel>();
        vm.Anime = anime;

        var result = await _contentDialogService.ShowDialog(vm, d =>
        {
            d.Title = anime.Title;
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

    public async Task<int> RequestRating(IAnimeModel anime)
    {
        var vm = new RequestRatingViewModel(anime);
        var result = await _contentDialogService.ShowDialog(vm, d =>
        {
            d.Title = "Update Rating";
            d.PrimaryButtonText = "Update";
            d.IsSecondaryButtonEnabled = false;
            d.CloseButtonText = "Cancel";
            d.Width = 300;
        });

        if (result == ContentDialogResult.Primary)
        {
            return vm.Rating;
        }

        return 0;
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

    public async Task<T> SelectModel<T>(IEnumerable<T> models, T defaultValue = default, Func<string, IObservable<IEnumerable<T>>> searcher = default)
        where T : class
    {
        var vm = new SelectModelViewModel<T>()
        {
            Models = models,
            SelectedModel = defaultValue,
            Search = searcher,
        };

        var result = await _contentDialogService.ShowDialog<SelectModelView, SelectModelViewModel<T>>(vm, d =>
        {
            d.Title = "Select";
            d.IsPrimaryButtonEnabled = true;
            d.PrimaryButtonText = "Ok";
            d.IsSecondaryButtonEnabled = false;
            d.CloseButtonText = "Cancel";
        });

        return result == ContentDialogResult.Primary ? vm.SelectedModel : null;
    }

    public async Task<long?> TryGetId(string title)
    {
        var candidates = Enumerable.Empty<AnimeModel>();
        try
        {
            candidates = await _animeService.GetAnime(title[..Math.Min(title.Length, 50)]);
        }
        catch (Exception ex)
        {
            this.Log().Error(ex);
            return null;
        }

        var filtered = candidates.Where(x => Fuzz.PartialRatio(x.Title, title) > 80 || x.AlternativeTitles.Any(x => Fuzz.PartialRatio(title, x) > 80)).ToList();
        if (filtered.Count == 1)
        {
            return filtered[0].Id;
        }
        else
        {
            if (!candidates.Any())
            {
                this.Log().Fatal($"no candidates found for title {title}");
                return null;
            }

            var model = await SelectModel(candidates, filtered.FirstOrDefault() ?? candidates.FirstOrDefault(), _animeService.GetAnime);
            return model?.Id;
        }
    }


    public async Task SubmitTimeStamp(long malId, int ep, VideoStreamModel stream, AniSkipResult existingResult, double duration, double introStart)
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
            Content = new MarkdownTextBlock() { Text = message, TextWrapping = TextWrapping.WrapWholeWords, Padding = new Thickness(10) },
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
            d.Title = $"{provider.DisplayName} {provider.Version}";
            d.IsPrimaryButtonEnabled = true;
            d.IsSecondaryButtonEnabled = true;
            d.PrimaryButtonText = "Save";
            d.SecondaryButtonText = "Cancel";
            d.PrimaryButtonCommand = vm.Save;
        });

        return Unit.Default;
    }

    public async Task<Unit> ConfigureOptions<T>(T type, Func<T, ProviderOptions> getFunc, Action<T, ProviderOptions> saveFunc)
    {
        var vm = new ConfigureOptionsViewModel<T>(getFunc, saveFunc)
        {
            Type = type,
            Title = type.ToString()
        };

        var result = await _contentDialogService.ShowDialog<ConfigureOptionsView, object>(vm, d =>
        {
            d.Title = vm.Title;
            d.IsPrimaryButtonEnabled = true;
            d.IsSecondaryButtonEnabled = true;
            d.PrimaryButtonText = "Save";
            d.SecondaryButtonText = "Cancel";
            d.PrimaryButtonCommand = vm.Save;
        });

        return Unit.Default;
    }


    public Task AddRssFeed()
    {
        return Task.CompletedTask;
    }

    public async Task<string> BrowseFolder()
    {
        // Create a folder picker
        FolderPicker openPicker = new();
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());

        // Set options for your folder picker
        openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
        openPicker.FileTypeFilter.Add("*");

        // Open the picker for the user to pick a folder
        StorageFolder folder = await openPicker.PickSingleFolderAsync();

        return folder is null
            ? string.Empty
            : folder.Path;
    }
}
