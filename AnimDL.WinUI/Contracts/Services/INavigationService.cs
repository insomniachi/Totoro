using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AnimDL.WinUI.Contracts.Services;

public interface INavigationService
{
    event NavigatedEventHandler Navigated;

    bool CanGoBack { get; }
    
    Frame Frame { get; set; }

    bool NavigateTo(string pageKey, IReadOnlyDictionary<string, object> parameter = null, bool clearNavigation = false);

    bool GoBack();
}
