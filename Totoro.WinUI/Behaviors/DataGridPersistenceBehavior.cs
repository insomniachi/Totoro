using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.Xaml.Interactivity;
using ReactiveMarbles.ObservableEvents;
using SharpCompress;
using System.Reflection;
using System.Text;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Behaviors;

public class DataGridPersistenceBehavior : Behavior<DataGrid>
{
    private readonly CompositeDisposable _disposables = new();
    
    public static readonly DependencyProperty SettingsProperty =
        DependencyProperty.Register("Settings", typeof(DataGridSettings), typeof(DataGridPersistenceBehavior), new PropertyMetadata(null));

    public DataGridSettings Settings
    {
        get { return (DataGridSettings)GetValue(SettingsProperty); }
        set { SetValue(SettingsProperty, value); }
    }

    public DataGridPersistenceBehavior()
    {
        this.WhenAnyValue(x => x.Settings)
            .WhereNotNull()
            .Subscribe(_ => ApplyDataGridSettings());

        this.WhenAnyValue(x => x.Settings)
            .WhereNotNull()
            .SelectMany(x => x.OnColumnVisibilityChanged())
            .Subscribe(column =>
            {
                if (AssociatedObject.Columns.FirstOrDefault(x => (string)x.Tag == column.Name) is not { } dgColumn)
                {
                    return;
                }

                AssociatedObject.DispatcherQueue.TryEnqueue(() =>
                {
                    dgColumn.Visibility = Converters.BooleanToVisibility(column.IsVisible);
                });
            });
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject
            .Events()
            .ColumnReordered
            .Where(_ => Settings is not null)
            .Subscribe(OnDisplayIndexChanged)
            .DisposeWith(_disposables);

        AssociatedObject
            .Events()
            .Sorting
            .Where(_ => Settings is not null)
            .Subscribe(OnSorting)
            .DisposeWith(_disposables);

        AssociatedObject
            .Events()
            .LoadingRow
            .Subscribe(x =>
            {
                x.Row.DoubleTapped += Row_DoubleTapped;
            })
            .DisposeWith(_disposables);

        AssociatedObject
            .Events()
            .UnloadingRow
            .Subscribe(x =>
            {
                x.Row.DoubleTapped += Row_DoubleTapped;
            })
            .DisposeWith(_disposables);

        ApplyDataGridSettings();
    }

    private void Row_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if(sender is not DataGridRow row)
        {
            return;
        }

        row.DetailsVisibility = row.DetailsVisibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    protected override void OnDetaching()
    {
        _disposables.Dispose();
    }

    private void OnDisplayIndexChanged(DataGridColumnEventArgs args)
    {
        foreach (var column in AssociatedObject.Columns)
        {
            var tag = (string)column.Tag;
            if (Settings.Columns.FirstOrDefault(x => x.Name == tag) is not { } model)
            {
                return;
            }
            model.DisplayIndex = column.DisplayIndex;
        }
    }

    private void OnSorting(DataGridColumnEventArgs args)
    {
        args.Column.SortDirection = args.Column.SortDirection is null or DataGridSortDirection.Descending
            ? DataGridSortDirection.Ascending
            : DataGridSortDirection.Descending;

        var tag = (string)args.Column.Tag;

        Settings.Sort = new DataGridSort(default, default)
        {
            ColumnName = tag, 
            IsAscending = args.Column.SortDirection == DataGridSortDirection.Ascending
        };

        foreach (var dgColumn in AssociatedObject.Columns.Where(x => x.Tag != args.Column.Tag))
        {
            dgColumn.SortDirection = null;
        }
    }

    private void ApplyDataGridSettings()
    {
        AssociatedObject.Columns.ForEach(x => x.Visibility = Visibility.Collapsed);

        foreach (var column in AssociatedObject.Columns.OrderBy(x => x.DisplayIndex))
        {
            var tag = (string)column.Tag;

            column.SortDirection = tag == Settings.Sort.ColumnName
                ? Settings.Sort.IsAscending
                    ? DataGridSortDirection.Ascending
                    : DataGridSortDirection.Descending
                : null;

            if (Settings.Columns.FirstOrDefault(x => x.Name == tag) is not { } model)
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
}
