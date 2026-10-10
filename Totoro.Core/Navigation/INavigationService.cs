using System.Threading.Tasks;

namespace Totoro.Core.Navigation;

public interface INavigator
{
	event EventHandler? Navigated;
	bool CanGoBack { get; }
	Task GoBackAsync();
	Task NavigateToKeyAsync(string key, object? parameter = null);
	Task NavigateToViewModelAsync(Type viewModelType, object? parameter = null);
	Task NavigateToDataAsync(object data);
}
