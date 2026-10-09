using Totoro.Core.Settings;

namespace Totoro.Core.Navigation;

public static class StartupPagePreferences
{
	private const string Key = "StartupPage";

	extension(IPreferences preferences)
	{
		/// <summary>
		/// Navigation key of the page shown at startup, i.e. the label it was registered with in
		/// <c>AddMainNavigationItem</c>. Null (or a key that no longer exists) means the first menu item.
		/// </summary>
		public string? StartupPage
		{
			get => preferences.ReadKey<string?>(Key);
			set => preferences.SaveKey(Key, value);
		}

		/// <summary>The item matching the saved startup page, falling back to the first non-footer item.</summary>
		public NavigationItemViewModel? FindStartupItem(IEnumerable<NavigationItemViewModel> items)
		{
			var startupPage = preferences.StartupPage;
			return items.FirstOrDefault(x => x.Label == startupPage) ?? items.FirstOrDefault(x => !x.IsFooter);
		}
	}
}
