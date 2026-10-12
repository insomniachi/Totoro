using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Navigation;
using Totoro.Core.Services;
using Totoro.Core.Settings;
using Totoro.Playback;
using Totoro.ViewModels.Chat;

namespace Totoro.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IRecipient<AnimeDatabaseChangedMessage>, IRecipient<OtherListSyncFailedMessage>
{
	private readonly IReadOnlyList<AccountViewModel> _accounts;

	public MainWindowViewModel(IEnumerable<NavigationItemViewModel> navigationItems, IEnumerable<IAccount> accounts, IPreferences preferences, WatchService playback, ChatViewModel chat, UpdateService updates)
	{
		Playback = playback;
		Chat = chat;
		Updates = updates;
		Updates.PropertyChanged += (_, e) =>
		{
			// Offered when one's found; and shown while it downloads, or if that fails.
			if (e.PropertyName == nameof(UpdateService.State))
			{
				IsUpdateBarOpen = Updates.State is UpdateState.Available or UpdateState.Downloading or UpdateState.Installing ||
								  Updates.State == UpdateState.Failed && Updates.Available is not null;
			}
		};
		_ = Updates.CheckOnStartupAsync();
		NavigationItems = [.. navigationItems.Where(x => !x.IsFooter)];
		FooterNavigationItems = [.. navigationItems.Where(x => x.IsFooter)];

		StartupItem = preferences.FindStartupItem([.. NavigationItems, .. FooterNavigationItems]);

		_accounts = [.. accounts.Select(x => new AccountViewModel(x))];
		ShowAccount(preferences.SelectedAnimeDatabase);
		WeakReferenceMessenger.Default.RegisterAll(this);
	}

	[ObservableProperty]
	public partial List<NavigationItemViewModel> NavigationItems { get; set; }

	[ObservableProperty]
	public partial List<NavigationItemViewModel> FooterNavigationItems { get; set; }

	/// <summary>The menu item to select when the app starts; see <see cref="StartupPagePreferences"/>.</summary>
	public NavigationItemViewModel? StartupItem { get; }

	/// <summary>What playing an episode is up to, for the status bar.</summary>
	public WatchService Playback { get; }

	/// <summary>The assistant, in a panel beside the pages.</summary>
	public ChatViewModel Chat { get; }

	/// <summary>Updating Totoro: a newer version is offered in a bar over the content.</summary>
	public UpdateService Updates { get; }

	[ObservableProperty]
	public partial bool IsUpdateBarOpen { get; set; }

	/// <summary>The account of the selected anime database, shown in the title bar.</summary>
	[ObservableProperty]
	public partial AccountViewModel? Account { get; private set; }

	public void Receive(AnimeDatabaseChangedMessage message)
	{
		ShowAccount(message.ModuleId);
	}

	/// <summary>Why a change to the user's list wasn't made on their other list, e.g. MyAnimeList; empty, never null, when it was.</summary>
	[ObservableProperty]
	public partial string SyncWarning { get; private set; } = "";

	[ObservableProperty]
	public partial bool IsSyncWarningOpen { get; set; }

	public void Receive(OtherListSyncFailedMessage message)
	{
		SyncWarning = message.Message;
		IsSyncWarningOpen = true;
	}

	private void ShowAccount(Guid databaseId)
	{
		// An unset (or unknown) database means the first one, as in the database factory and Settings.
		Account = _accounts.FirstOrDefault(x => x.Id == databaseId) ?? _accounts.FirstOrDefault();
	}
}
