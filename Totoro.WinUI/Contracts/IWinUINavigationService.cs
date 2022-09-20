using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Totoro.WinUI.Contracts;

public interface IWinUINavigationService : INavigationService
{
    IObservable<NavigationEventArgs> Navigated { get; }
    Frame Frame { get; set; }
}
