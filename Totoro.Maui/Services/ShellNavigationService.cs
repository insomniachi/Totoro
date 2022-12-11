using Totoro.Core.Contracts;

namespace Totoro.Maui.Services
{
    internal class ShellNavigationService : INavigationService
    {
        public bool CanGoBack => throw new NotImplementedException();

        public bool GoBack()
        {
            throw new NotImplementedException();
        }

        public bool NavigateTo(Type viewModelType, object viewModel = null, IReadOnlyDictionary<string, object> parameter = null, bool clearNavigation = false)
        {
            throw new NotImplementedException();
        }

        bool INavigationService.NavigateTo<TViewModel>(TViewModel viewModel, IReadOnlyDictionary<string, object> parameter, bool clearNavigation)
        {
            throw new NotImplementedException();
        }
    }
}
