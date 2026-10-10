using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.Core.Navigation;
using Totoro.Core.Settings;

namespace Totoro.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IRecipient<AnimeDatabaseChangedMessage>
{
	private readonly IReadOnlyList<AccountViewModel> _accounts;

	public MainWindowViewModel(IEnumerable<NavigationItemViewModel> navigationItems, IEnumerable<IAccount> accounts, IPreferences preferences)
	{
		NavigationItems = [.. navigationItems.Where(x => !x.IsFooter)];
		FooterNavigationItems = [.. navigationItems.Where(x => x.IsFooter)];

		StartupItem = preferences.FindStartupItem([.. NavigationItems, .. FooterNavigationItems]);

		_accounts = [.. accounts.Select(x => new AccountViewModel(x))];
		ShowAccount(preferences.SelectedAnimeDatabase);
		WeakReferenceMessenger.Default.Register(this);
	}

	[ObservableProperty]
	public partial List<NavigationItemViewModel> NavigationItems { get; set; }

	[ObservableProperty]
	public partial List<NavigationItemViewModel> FooterNavigationItems { get; set; }

	/// <summary>The menu item to select when the app starts; see <see cref="StartupPagePreferences"/>.</summary>
	public NavigationItemViewModel? StartupItem { get; }

	/// <summary>The account of the selected anime database, shown in the title bar.</summary>
	[ObservableProperty]
	public partial AccountViewModel? Account { get; private set; }

	public void Receive(AnimeDatabaseChangedMessage message)
	{
		ShowAccount(message.ModuleId);
	}

	private void ShowAccount(Guid databaseId)
	{
		// An unset (or unknown) database means the first one, as in the database factory and Settings.
		Account = _accounts.FirstOrDefault(x => x.Id == databaseId) ?? _accounts.FirstOrDefault();
	}
}
