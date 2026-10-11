using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules;

namespace Totoro.ViewModels;

/// <summary>
/// An account as shown in Settings and the title bar: whether (and as whom) the user is signed in, and a command to
/// sign in or out. Every instance for the same account updates when any of them signs in or out.
/// </summary>
public sealed partial class AccountViewModel : ObservableObject, IRecipient<AccountChangedMessage>
{
	private readonly IAccount _account;

	public AccountViewModel(IAccount account)
	{
		_account = account;
		Name = account.Name;
		WeakReferenceMessenger.Default.Register(this);
		_ = RefreshAsync();
	}

	public Guid Id => _account.Id;

	public string Name { get; }

	[ObservableProperty] public partial string Status { get; private set; } = "";

	/// <summary>The signed-in user's name; null when not signed in.</summary>
	[ObservableProperty] public partial string? UserName { get; private set; }

	/// <summary>
	/// URL of the signed-in user's picture; empty, never null, when there isn't one. x:Bind doesn't call a function
	/// whose argument is null (e.g. <c>ToImageSource(Account.Avatar)</c>), so a null would leave the previous picture up.
	/// </summary>
	[ObservableProperty] public partial string Avatar { get; private set; } = "";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ActionName))]
	public partial string ActionText { get; private set; } = "";

	/// <summary>The button's accessible name; says which account, since every card has the same button.</summary>
	public string ActionName => $"{ActionText} ({Name})";

	public void Receive(AccountChangedMessage message)
	{
		if (message.AccountId == Id)
		{
			_ = RefreshAsync();
		}
	}

	[RelayCommand]
	private async Task SignInOrOutAsync()
	{
		if (_account.IsSignedIn)
		{
			_account.SignOut();
		}
		else
		{
			Status = "Signing in…";
			if (!await _account.SignInAsync())
			{
				await RefreshAsync();
				Status = "Not signed in. Sign-in was cancelled or didn't complete.";
				return;
			}
		}

		// Refreshes this and every other view of the account.
		WeakReferenceMessenger.Default.Send(new AccountChangedMessage(Id));
	}

	private async Task RefreshAsync()
	{
		if (!_account.IsSignedIn)
		{
			Status = "Not signed in";
			ActionText = "Sign in";
			UserName = null;
			Avatar = "";
			return;
		}

		ActionText = "Sign out";
		Status = "Signed in";
		var user = await _account.GetUserAsync();
		UserName = user?.Name;
		Avatar = user?.Avatar ?? "";
		if (user is not null)
		{
			Status = $"Signed in as {user.Name}";
		}
	}
}
