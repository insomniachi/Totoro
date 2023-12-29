using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.WinUI.UserControls;

public class ReactiveContentDialog<TViewModel> : ContentDialog, IViewFor<TViewModel>
    where TViewModel : class, INotifyPropertyChanged
{
    public TViewModel ViewModel
    {
        get { return (TViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    object IViewFor.ViewModel 
    {
        get => ViewModel;
        set
        {
            if (value is TViewModel vm)
            {
                ViewModel = vm;
            }
        }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register("ViewModel", typeof(TViewModel), typeof(ReactiveContentDialog<TViewModel>), new PropertyMetadata(null));


}
