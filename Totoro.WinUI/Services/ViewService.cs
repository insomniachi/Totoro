using CommunityToolkit.WinUI.UI.Controls;
using FuzzySharp;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Splat;
using Totoro.Plugins;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Dialogs.ViewModels;
using Totoro.WinUI.Dialogs.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinUIEx;

namespace Totoro.WinUI.Services;

public class ViewService : IViewService, IEnableLogger
{
    private readonly IContentDialogService _contentDialogService;
    private readonly IAnimeServiceContext _animeService;
    private readonly IToastService _toastService;
    private readonly ICommand _copyToClipboard;


    public ViewService(IContentDialogService contentDialogService,
                       IAnimeServiceContext animeService,
                       IToastService toastService)
    {
        _contentDialogService = contentDialogService;
        _animeService = animeService;
        _toastService = toastService;
        _copyToClipboard = ReactiveCommand.Create<Exception>(ex =>
        {
            var package = new DataPackage();
            package.SetText(ex.ToString());
            Clipboard.SetContent(package);
        });
    }

    public async Task<Unit> UpdateTracking(IAnimeModel anime)
    {
        var vm = App.GetService<UpdateAnimeStatusViewModel>();
        vm.Anime = anime;

        var result = await _contentDialogService.ShowDialog(vm, d =>
        {
            d.Title = anime.Title;
            d.PrimaryButtonText = "Update";
            d.PrimaryButtonCommand = vm.Update;
            d.SecondaryButtonText = "Delete";
            d.IsSecondaryButtonEnabled = true;
            d.SecondaryButtonCommand = vm.Delete;
            d.CloseButtonText = "Cancel";
        });

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

    public async Task<ICatalogItem> ChoooseSearchResult(ICatalogItem closestMatch, List<ICatalogItem> searchResults, string providerType)
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

        if (!candidates.Any())
        {
            this.Log().Fatal($"no candidates found for title {title}");
            return null;
        }

        if (candidates.FirstOrDefault(x => x.Title == title || x.AlternativeTitles.Any(x => x == title)) is { } result)
        {
            return result.Id;
        }

        var ratios = candidates.Select(x => (x, Fuzz.PartialRatio(x.Title, title))).OrderByDescending(x => x.Item2).ToList();
        var filtered = ratios.Where(x => x.Item2 > 80).ToList();
        if (filtered.Count == 1)
        {
            return filtered[0].x.Id;
        }
        else
        {
            var hundredPercent = filtered.Where(x => x.Item2 == 100).ToList();
            if (hundredPercent.Count == 1)
            {
                return hundredPercent[0].x.Id;
            }

            var model = await SelectModel(candidates, filtered.FirstOrDefault().x ?? candidates.FirstOrDefault(), _animeService.GetAnime);
            return model?.Id;
        }
    }

    public async Task<long?> BeginTryGetId(string title)
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

        if (!candidates.Any())
        {
            this.Log().Fatal($"no candidates found for title {title}");
            return null;
        }

        if (candidates.FirstOrDefault(x => x.Title == title || x.AlternativeTitles.Any(x => x == title)) is { } result)
        {
            return result.Id;
        }

        var ratios = candidates.Select(x => (x, Fuzz.Ratio(x.Title, title))).OrderByDescending(x => x.Item2).ToList();
        var filtered = ratios.Where(x => x.Item2 > 80).ToList();
        if (filtered.Count == 1)
        {
            return filtered[0].x.Id;
        }
        else
        {
            var hundredPercent = filtered.Where(x => x.Item2 == 100).ToList();
            if (hundredPercent.Count == 1)
            {
                return hundredPercent[0].x.Id;
            }

            _toastService.PromptAnimeSelection(candidates, filtered.FirstOrDefault().x ?? candidates.FirstOrDefault());

            return null;
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

    public async Task UnhandledException(Exception ex)
    {
        var content = new Grid()
        {
            Padding = new Thickness(10),
            Children =
            {
                new TextBlock
                {
                    Text = ex.ToString(),
                    TextWrapping = TextWrapping.WrapWholeWords
                }
            }
        };

        var dialog = new ContentDialog()
        {
            Title = "Unhandled Exception",
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            XamlRoot = App.MainWindow.Content.XamlRoot,
            DefaultButton = ContentDialogButton.Primary,
            Content = content,
            PrimaryButtonText = "Okay",
            SecondaryButtonText = "Copy To Clipboard",
            SecondaryButtonCommand = _copyToClipboard,
            SecondaryButtonCommandParameter = ex
        };

        await dialog.ShowAsync();
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

        await dialog.ShowAsync();
        return Unit.Default;
    }

    public async Task<Unit> ConfigureProvider(PluginInfo provider)
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

    public async Task<Unit> ConfigureOptions<T>(T type, Func<T, PluginOptions> getFunc, Action<T, PluginOptions> saveFunc)
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

    public async Task<string> BrowseSubtitle()
    {
        // Create a file picker
        var openPicker = new FileOpenPicker();

        // Initialize the file picker with the window handle (HWND).
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());

        // Set options for your file picker
        openPicker.ViewMode = PickerViewMode.Thumbnail;
        openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        openPicker.FileTypeFilter.Add(".srt");
        openPicker.FileTypeFilter.Add(".vtt");
        openPicker.FileTypeFilter.Add(".ssa");
        openPicker.FileTypeFilter.Add(".ass");

        // Open the picker for the user to pick a file
        var file = await openPicker.PickSingleFileAsync();
        return file?.Path ?? string.Empty;
    }
}
