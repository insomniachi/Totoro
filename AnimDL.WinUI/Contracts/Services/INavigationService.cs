using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AnimDL.WinUI.Contracts.Services;

public interface INavigationService
{
    IObservable<NavigationEventArgs> Navigated { get; }
    
    bool CanGoBack { get; }
    
    Frame Frame { get; set; }
    
    bool NavigateTo(Type viewModelType, object viewModel = null, IReadOnlyDictionary<string, object> parameter = null, bool clearNavigation = false);
  
    bool NavigateTo<TViewModel>(TViewModel viewModel = null, IReadOnlyDictionary<string, object> parameter = null, bool clearNavigation = false)
        where TViewModel : class, INotifyPropertyChanged;

    bool GoBack();
}
