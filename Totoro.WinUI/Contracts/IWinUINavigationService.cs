using System.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace Totoro.WinUI.Contracts;

public interface IWinUINavigationService : INavigationService
{
    IObservable<NavigationEventArgs> Navigated { get; }
    bool NavigateTo(Type viewModelType, 
                    object viewModel = null,
                    IReadOnlyDictionary<string, object> parameter = null,
                    bool clearNavigation = false,
                    NavigationTransitionInfo transitionInfo = null);
    Frame Frame { get; set; }
}
