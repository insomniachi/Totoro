using Totoro.Core.Contracts;

namespace Totoro.Maui.Services
{
    internal class ShellNavigationService : INavigationService
    {
        public ShellNavigationService()
        {

        }

        internal void Initialize()
        {
            Shell.Current.NavigatingFrom += Current_NavigatingFrom;
            Shell.Current.NavigatedTo += Current_NavigatedTo;
        }

        private void Current_NavigatedTo(object sender, NavigatedToEventArgs e)
        {
            ;
        }

        private void Current_NavigatingFrom(object sender, NavigatingFromEventArgs e)
        {
            ;
        }

        public bool CanGoBack => true;

        public bool GoBack()
        {
            return true;
        }

        public bool NavigateTo(Type viewModelType, object viewModel = null, IReadOnlyDictionary<string, object> parameter = null, bool clearNavigation = false)
        {
            Shell.Current.GoToAsync(viewModelType.Name, parameters: parameter.ToDictionary(x => x.Key, x => x.Value));
            return true;
        }

        bool INavigationService.NavigateTo<TViewModel>(TViewModel viewModel, IReadOnlyDictionary<string, object> parameter, bool clearNavigation)
        {
            Shell.Current.GoToAsync(viewModel.GetType().Name, parameters: parameter?.ToDictionary(x => x.Key, x => x.Value));
            return true;
        }
    }
}
