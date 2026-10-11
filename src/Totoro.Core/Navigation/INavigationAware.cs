namespace Totoro.Core.Navigation;

public interface INavigationAware
{
	Task OnNavigatedToAsync();
	Task OnNavigatedFromAsync();
}
