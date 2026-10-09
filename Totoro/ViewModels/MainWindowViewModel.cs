using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Navigation;
using Totoro.Core.Settings;

namespace Totoro.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
	public MainWindowViewModel(IEnumerable<NavigationItemViewModel> navigationItems, IPreferences preferences)
	{
		NavigationItems = [.. navigationItems.Where(x => !x.IsFooter)];
		FooterNavigationItems = [.. navigationItems.Where(x => x.IsFooter)];

		StartupItem = preferences.FindStartupItem([.. NavigationItems, .. FooterNavigationItems]);
	}

	[ObservableProperty]
	public partial List<NavigationItemViewModel> NavigationItems { get; set; }

	[ObservableProperty]
	public partial List<NavigationItemViewModel> FooterNavigationItems { get; set; }

	/// <summary>The menu item to select when the app starts; see <see cref="StartupPagePreferences"/>.</summary>
	public NavigationItemViewModel? StartupItem { get; }
}
