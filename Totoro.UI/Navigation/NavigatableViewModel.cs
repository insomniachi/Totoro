using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Navigation;

namespace Totoro.UI.Navigation;

public abstract class NavigatableViewModel : ObservableObject, INavigationAware
{
	public async Task OnNavigatedFrom()
	{
		await OnNavigatedFromInternal();
	}

	public async Task OnNavigatedTo()
	{
		await OnNavigatedToInternal();
	}

	protected virtual Task OnNavigatedToInternal() => Task.CompletedTask;
	protected virtual Task OnNavigatedFromInternal() => Task.CompletedTask;
}
