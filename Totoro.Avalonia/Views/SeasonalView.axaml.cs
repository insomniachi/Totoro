using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Totoro.Core.ViewModels;

namespace Totoro.Avalonia.Views;

public partial class SeasonalView : ReactiveUserControl<SeasonalViewModel>
{
    public SeasonalView()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            SeasonFilterControl.SelectedItem = SeasonFilterControl.Items.OfType<TabStripItem>()
                .FirstOrDefault(x => x.Content?.ToString() == ViewModel!.SeasonFilter);
        });
    }

    private void SeasonFilterControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }
        
        var selectedItem = e.AddedItems.OfType<TabStripItem>().First();
        ViewModel.SetSeasonCommand.Execute(selectedItem.Content as string);
    }
}