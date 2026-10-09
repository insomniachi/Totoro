using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Navigation;
using Totoro.Core.Settings;

namespace Totoro.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
	private readonly IPreferences _preferences;

	public SettingsViewModel(IEnumerable<NavigationItemViewModel> navigationItems, IPreferences preferences)
	{
		_preferences = preferences;

		// Footer items (like Settings itself) aren't offered as a startup page.
		StartupPages = [.. navigationItems.Where(x => !x.IsFooter)];
		StartupPage = preferences.FindStartupItem(StartupPages);
	}

	public IReadOnlyList<NavigationItemViewModel> StartupPages { get; }

	[ObservableProperty]
	public partial NavigationItemViewModel? StartupPage { get; set; }

	partial void OnStartupPageChanged(NavigationItemViewModel? value)
	{
		if (value is not null && value.Label != _preferences.StartupPage)
		{
			_preferences.StartupPage = value.Label;
		}
	}
}
