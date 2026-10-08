using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Navigation;

namespace Totoro.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
	public MainWindowViewModel(IEnumerable<NavigationItemViewModel> navigationItems)
	{
		NavigationItems = [.. navigationItems.Where(x => !x.IsFooter)];
		FooterNavigationItems = [.. navigationItems.Where(x => x.IsFooter)];
	}

	[ObservableProperty]
	public partial List<NavigationItemViewModel> NavigationItems { get; set; }

	[ObservableProperty]
	public partial List<NavigationItemViewModel> FooterNavigationItems { get; set; }
}
