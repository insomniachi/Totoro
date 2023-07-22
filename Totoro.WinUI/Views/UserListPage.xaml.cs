using System.Globalization;
using System.Windows;
using CommunityToolkit.Labs.WinUI;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ReactiveMarbles.ObservableEvents;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Views;

public class UserListPageBase : ReactivePage<UserListViewModel> { }
public sealed partial class UserListPage : UserListPageBase
{
    private readonly ILocalSettingsService _localSettingsService;
    public const string DataGridSettingsKey = "UserListDataGridSettings";

    public List<AnimeStatus> Statuses { get; set; } = new List<AnimeStatus>
    {
        AnimeStatus.Watching,
        AnimeStatus.PlanToWatch,
        AnimeStatus.OnHold,
        AnimeStatus.Completed,
        AnimeStatus.Dropped
    };

    public static string ToStatusString(AnimeStatus status)
    {
        return status switch
        {
            AnimeStatus.Watching => "Watching",
            AnimeStatus.PlanToWatch => "Plan to watch",
            AnimeStatus.OnHold => "On Hold",
            AnimeStatus.Completed => "Completed",
            AnimeStatus.Dropped => "Dropped",
            _ => throw new ArgumentException(null, nameof(status))
        };
    }

    public DataGridSettings DataGridSettings { get; private set; }

    public UserListPage()
    {
        _localSettingsService = App.GetService<ILocalSettingsService>();
        DataGridSettings = _localSettingsService.ReadSetting(DataGridSettingsKey, new DataGridSettings { Columns = GetDefaultColumns() });
        var defaultColumns = GetDefaultColumns();
        var newColumnsAdded = false;
        foreach (var item in defaultColumns)
        {
            if(DataGridSettings.Columns.FirstOrDefault(x => x.Name == item.Name) is not null)
            {
                continue;
            }

            newColumnsAdded = true;
            DataGridSettings.Columns.Add(item);
        }

        if(newColumnsAdded)
        {
            SaveDataGridSettings();
        }

        InitializeComponent();

        this.WhenActivated(d =>
        {
            ViewModel.SortSettings = DataGridSettings.Sort;

            DataGridSettings
            .OnColumnChanged()
            .Subscribe(_ => SaveDataGridSettings())
            .DisposeWith(ViewModel.Garbage);

            DataGridSettings.OnColumnVisibilityChanged().Subscribe(column =>
            {
                if (DataGrid.Columns.FirstOrDefault(x => (string)x.Tag == column.Name) is not { } dgColumn)
                {
                    return;
                }

                DataGrid.DispatcherQueue.TryEnqueue(() =>
                {
                    dgColumn.Visibility = Converters.BooleanToVisibility(column.IsVisible);
                });

            }).DisposeWith(ViewModel.Garbage);

            this.WhenAnyValue(x => x.ViewModel.SortSettings)
                .WhereNotNull()
                .Where(x => x != DataGridSettings.Sort)
                .Subscribe(settings =>
                {
                    DataGridSettings.Sort = settings;
                    SaveDataGridSettings();
                });

            this.WhenAnyValue(x => x.ViewModel.Filter.ListStatus)
                .Subscribe(x =>
                {
                    switch (x)
                    {
                        case AnimeStatus.Watching:
                            WatchingFlyoutToggle.IsChecked = true;
                            break;
                        case AnimeStatus.PlanToWatch:
                            PtwFlyoutToggle.IsChecked = true;
                            break;
                        case AnimeStatus.Completed:
                            CompletedFlyoutToggle.IsChecked = true;
                            break;
                        case AnimeStatus.OnHold:
                            OnHoldFlyoutToggle.IsChecked = true;
                            break;
                        case AnimeStatus.Dropped:
                            DroppedFlyoutToggle.IsChecked = true;
                            break;
                    }
                })
                .DisposeWith(d);

            QuickAdd
            .Events()
            .Click
            .Do(_ => ViewModel.ClearSearch())
            .Subscribe(_ => QuickAddPopup.IsOpen ^= true)
            .DisposeWith(d);

            GenresButton
            .Events()
            .Click
            .Subscribe(_ => GenresTeachingTip.IsOpen ^= true);

            QuickAddResult
            .Events()
            .ItemClick
            .Select(args => args.ClickedItem as IAnimeModel)
            .Do(_ => QuickAddPopup.IsOpen = false)
            .SelectMany(model => ViewModel.UpdateAnime(model))
            .Subscribe()
            .DisposeWith(d);
        });
    }

    private void GenresButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        GenresTeachingTip.IsOpen = true;
    }

    private void TokenView_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        ViewModel.Filter.Genres = new ObservableCollection<string>(((TokenView)sender).SelectedItems.Cast<string>());
    }

    private void AiringStatusClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var btn = (RadioMenuFlyoutItem)sender;
        ViewModel.Filter.AiringStatus = btn.Tag is AiringStatus status ? status : (AiringStatus?)null;
    }

    private void DataGrid_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => ApplyDataGridSettings();

    private void DataGrid_ColumnDisplayIndexChanged(object sender, DataGridColumnEventArgs e)
    {
        var tag = (string)e.Column.Tag;
        if (DataGridSettings.Columns.FirstOrDefault(x => x.Name == tag) is not { } model)
        {
            return;
        }
        model.DisplayIndex = e.Column.DisplayIndex;
    }

    private static List<DataGridColumnModel> GetDefaultColumns()
    {
        return new List<DataGridColumnModel>()
        {
            new()
            {
                Name = "Title",
                DisplayIndex = 0,
                Width = 600
            },
            new()
            {
                Name = "Season",
                DisplayIndex = 1,
            },
            new()
            {
                Name = "Mean Score",
                DisplayIndex = 2,
            },
            new()
            {
                Name = "User Score",
                DisplayIndex = 3,
            },
            new()
            {
                Name = "Tracking",
                DisplayIndex = 4,
            },
            new()
            {
                Name = "Date Started",
                DisplayIndex = 5,
                IsVisible = false
            },
            new()
            {
                Name = "Date Completed",
                DisplayIndex = 6,
                IsVisible = false
            },
            new()
            {
                Name = "Last Updated",
                DisplayIndex = 7,
                IsVisible = false
            }
        };
    }

    private void OnSaveColumnWidths(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        foreach (var column in DataGrid.Columns)
        {
            if(DataGridSettings.Columns.FirstOrDefault(x => x.Name == (string)column.Tag) is not { } model)
            {
                continue;
            }

            model.Width = column.ActualWidth;
        }
    }

    private void ResetDataGridSettings(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        DataGridSettings = new DataGridSettings { Columns = GetDefaultColumns() };
        SaveDataGridSettings();
        ApplyDataGridSettings();
    }

    private void ApplyDataGridSettings()
    {
        foreach (var column in DataGrid.Columns)
        {
            var tag = (string)column.Tag;
            
            column.SortDirection = tag == DataGridSettings.Sort.ColumnName 
                ? DataGridSettings.Sort.IsAscending
                    ? DataGridSortDirection.Ascending 
                    : DataGridSortDirection.Descending 
                : null;

            if (DataGridSettings.Columns.FirstOrDefault(x => x.Name == tag) is not { } model)
            {
                continue;
            }

            column.DisplayIndex = model.DisplayIndex;
            column.Visibility = Converters.BooleanToVisibility(model.IsVisible);
            column.Width = model.Width is null
                ? DataGridLength.Auto
                : new DataGridLength(model.Width.Value, DataGridLengthUnitType.Pixel);
        }
    }

    private void DataGrid_Sorting(object sender, DataGridColumnEventArgs e)
    {
        if (e.Column.SortDirection == null || e.Column.SortDirection == DataGridSortDirection.Descending)
        {
            e.Column.SortDirection = DataGridSortDirection.Ascending;
        }
        else
        {
            e.Column.SortDirection = DataGridSortDirection.Descending;
        }

        var tag = (string)e.Column.Tag;
        
        ViewModel.SortSettings = new SortSettings { ColumnName = tag, IsAscending = e.Column.SortDirection == DataGridSortDirection.Ascending };

        foreach (var dgColumn in DataGrid.Columns.Where(x => x.Tag != e.Column.Tag))
        {
            dgColumn.SortDirection = null;
        }
    }

    private void SaveDataGridSettings()
    {
        _localSettingsService.SaveSetting(DataGridSettingsKey, DataGridSettings);
    }
}

public class AiringStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if(value is not AiringStatus status)
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

public class DataGridSettings : ReactiveObject
{
    public SortSettings Sort { get; set; } = new() { ColumnName = "Title", IsAscending = true };
    public List<DataGridColumnModel> Columns { get; set; }

    public IObservable<DataGridColumnModel> OnColumnChanged()
    {
        var observables = Columns.Select(x => x.WhenAnyPropertyChanged());
        return Observable.Merge(observables).Throttle(TimeSpan.FromSeconds(1));
    }

    public IObservable<DataGridColumnModel> OnColumnVisibilityChanged()
    {
        return Observable.Merge(Columns.Select(x => x.WhenAnyPropertyChanged(nameof(DataGridColumnModel.IsVisible))));
    }
}

public class DataGridColumnModel : ReactiveObject
{
    public string Name { get; set; }
    [Reactive] public bool IsVisible { get; set; } = true;
    [Reactive] public int DisplayIndex { get; set; }
    [Reactive] public double? Width { get; set; }
}



