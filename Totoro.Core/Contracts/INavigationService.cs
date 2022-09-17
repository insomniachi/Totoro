using System.ComponentModel;

namespace Totoro.Core.Contracts;

public interface INavigationService
{
    bool CanGoBack { get; }

    bool NavigateTo(Type viewModelType, object viewModel = null, IReadOnlyDictionary<string, object> parameter = null, bool clearNavigation = false);

    bool NavigateTo<TViewModel>(TViewModel viewModel = null, IReadOnlyDictionary<string, object> parameter = null, bool clearNavigation = false)
        where TViewModel : class, INotifyPropertyChanged;

    bool GoBack();
}
