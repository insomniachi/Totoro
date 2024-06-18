using CommunityToolkit.Labs.WinUI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using ReactiveMarbles.ObservableEvents;
using Totoro.Core;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views;

public class UserListPageBase : ReactivePage<UserListViewModel> { }
public sealed partial class UserListPage : UserListPageBase
{
    public static DataGridSettings TableViewSettings { get; private set; }

    public static List<int?> Scores { get; } = [null, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    public static string ToStatusString(AnimeStatus status)
    {
        return status switch
        {
            AnimeStatus.Watching => "Watching",
            AnimeStatus.PlanToWatch => "Plan to watch",
            AnimeStatus.OnHold => "On Hold",
            AnimeStatus.Completed => "Completed",
            AnimeStatus.Dropped => "Dropped",
            AnimeStatus.Rewatching => "Rewatching",
            _ => throw new ArgumentException(null, nameof(status))
        };
    }

    public ICommand ViewInBrowser { get; }

    public UserListPage()
    {
        TableViewSettings = SettingsModel.UserListTableViewSettings;

        InitializeComponent();

        this.WhenActivated(d =>
        {
            QuickAdd
            .Events()
            .Click
            .Subscribe(async _ => await ViewModel.ShowSearchDialog())
            .DisposeWith(d);

            GenresButton
            .Events()
            .Click
            .Subscribe(_ => GenresTeachingTip.IsOpen ^= true);

            AppearanceButton
            .Events()
            .Click
            .Subscribe(_ => GridViewSettingsTeachingTip.IsOpen ^= true);

            this.WhenAnyValue(x => x.ViewModel.Mode)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(mode =>
                {
                    AnimeCollectionDataTemplateSelector.Mode = mode;
                    AnimeCollectionView.Layout = CreateLayout(mode);
                });
        });

        ViewInBrowser = ReactiveCommand.CreateFromTask<AnimeModel>(LaunchUrl);
    }

    private Layout CreateLayout(DisplayMode mode)
    {
        return mode switch
        {
            DisplayMode.Grid => CreateUniformGridLayout(ViewModel.GridViewSettings),
            DisplayMode.List => new StackLayout() { Orientation = Orientation.Vertical, Spacing = 3 },
            _ => throw new NotSupportedException()
        };
    }

    private async void ImageTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        var anime = ((Image)sender).DataContext as AnimeModel;
        await LaunchUrl(anime);
    }

    private async Task LaunchUrl(AnimeModel anime)
    {
        var url = ViewModel.ListType switch
        {
            ListServiceType.MyAnimeList => $@"https://myanimelist.net/anime/{anime.Id}/",
            ListServiceType.AniList => $@"https://anilist.co/anime/{anime.Id}/",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
    }

    private void TokenView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.Filter.Genres = new ObservableCollection<string>(((TokenView)sender).SelectedItems.Cast<string>());
    }

    private void AiringStatusClicked(object sender, RoutedEventArgs e)
    {
        var btn = (RadioMenuFlyoutItem)sender;
        ViewModel.Filter.AiringStatus = btn.Tag is AiringStatus status ? status : (AiringStatus?)null;
    }

    private static UniformGridLayout CreateUniformGridLayout(GridViewSettings settings)
    {
        BindingBase CreateBinding(string path)
        {
            return new Binding()
            {
                Source = settings,
                Path = new PropertyPath(path),
                Mode = BindingMode.OneWay
            };
        }


        var layout = new UniformGridLayout() { ItemsStretch = UniformGridLayoutItemsStretch.Fill };
        BindingOperations.SetBinding(layout, UniformGridLayout.MaximumRowsOrColumnsProperty, CreateBinding(nameof(settings.MaxNumberOfColumns)));
        BindingOperations.SetBinding(layout, UniformGridLayout.MinRowSpacingProperty, CreateBinding(nameof(settings.SpacingBetweenItems)));
        BindingOperations.SetBinding(layout, UniformGridLayout.MinColumnSpacingProperty, CreateBinding(nameof(settings.SpacingBetweenItems)));
        BindingOperations.SetBinding(layout, UniformGridLayout.MinItemHeightProperty, CreateBinding(nameof(settings.ItemHeight)));
        BindingOperations.SetBinding(layout, UniformGridLayout.MinItemWidthProperty, CreateBinding(nameof(settings.DesiredWidth)));
        return layout;
    }

    private void ResetColumns(object sender, RoutedEventArgs e)
    {
        var defaultSettings = Settings.GetDefaultUserListDataGridSettings();

        using var delay = TableViewSettings.DelayChangeNotifications();

        foreach (var item in TableViewSettings.Columns)
        {
            if(defaultSettings.Columns.FirstOrDefault(x => x.Name == item.Name) is not { } col)
            {
                continue;
            }

            item.Width = col.Width;
            item.IsVisible = col.IsVisible;
        }
    }
}


public class AiringStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not AiringStatus status)
        {
            return DependencyProperty.UnsetValue;
        }

        return status switch
        {
            AiringStatus.CurrentlyAiring => new SolidColorBrush(Colors.LimeGreen),
            AiringStatus.FinishedAiring => new SolidColorBrush(Colors.MediumSlateBlue),
            AiringStatus.NotYetAired => new SolidColorBrush(Colors.LightSlateGray),
            _ => new SolidColorBrush(Colors.Navy),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class AnimeCollectionDataTemplateSelector : DataTemplateSelector
{
    public DataTemplate GridTemplate { get; set; }
    public DataTemplate TableTemplate { get; set; }

    public static DisplayMode Mode { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return Mode switch
        {
            DisplayMode.Grid => GridTemplate,
            DisplayMode.List => TableTemplate,
            _ => base.SelectTemplateCore(item)
        };
    }
}



