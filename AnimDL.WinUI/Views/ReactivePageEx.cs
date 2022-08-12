using System.ComponentModel;
using ReactiveUI;

namespace AnimDL.WinUI.Views;

public class ReactivePageEx<TViewModel> : ReactivePage<TViewModel>
    where TViewModel : class, INotifyPropertyChanged
{
    public ReactivePageEx()
    {
        //ViewModel = App.GetService<TViewModel>();
    }
}
