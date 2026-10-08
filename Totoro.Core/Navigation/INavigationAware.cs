namespace Totoro.Core.Navigation;

public interface INavigationAware
{
	Task OnNavigatedTo();
	Task OnNavigatedFrom();
}
