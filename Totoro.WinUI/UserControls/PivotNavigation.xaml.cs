using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Contracts;

namespace Totoro.WinUI.UserControls;

public sealed partial class PivotNavigation : UserControl
{
    private IWinUINavigationService _navigationService;

    public PivotItemModel SelectedItem
    {
        get { return (PivotItemModel)GetValue(SelectedItemProperty); }
        set { SetValue(SelectedItemProperty, value); }
    }

    public IEnumerable<PivotItemModel> ItemSource
    {
        get { return (IEnumerable<PivotItemModel>)GetValue(ItemSourceProperty); }
        set { SetValue(ItemSourceProperty, value); }
    }

    public string SectionGroupName
    {
        get { return (string)GetValue(SectionGroupNameProperty); }
        set { SetValue(SectionGroupNameProperty, value); }
    }

    public static readonly DependencyProperty SectionGroupNameProperty =
        DependencyProperty.Register("SectionGroupName", typeof(string), typeof(PivotNavigation), new PropertyMetadata("", OnSectionGroupNameChanged));

    public static readonly DependencyProperty ItemSourceProperty =
        DependencyProperty.Register("ItemSource", typeof(IEnumerable<PivotItemModel>), typeof(PivotNavigation), new PropertyMetadata(Enumerable.Empty<PivotItemModel>()));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register("SelectedItem", typeof(PivotItemModel), typeof(PivotNavigation), new PropertyMetadata(null, OnSelectedItemChanged));

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PivotNavigation)d;
        if(e.NewValue is not PivotItemModel { } selectedItem)
        {
            return;
        }

        control._navigationService.NavigateTo(selectedItem.ViewModel);
    }

    private static void OnSectionGroupNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PivotNavigation)d;
        control._navigationService = App.GetService<IWinUINavigationService>(e.NewValue);
        control._navigationService.Frame = control.NavFrame;
    }



    public PivotNavigation()
    {
        InitializeComponent();
    }
}
