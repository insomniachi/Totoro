using System.Threading.Tasks;

namespace Totoro.Core.Navigation;

public interface INavigator
{
	event EventHandler? Navigated;
	Task NavigateToKey(string key, object? parameter = null);
	Task NavigateToViewModel(Type viewModelType, object? parameter = null);
	Task NavigateToData(object data);
}
