namespace Totoro.Core.Module;

/// <summary>
/// The user's account with a module's online service (e.g. AniList). Modules register implementations with
/// <c>AddTransient&lt;IAccount, ...&gt;</c>; Settings and the title bar list them so the user can sign in and out.
/// </summary>
public interface IAccount
{
	/// <summary>The module's id.</summary>
	Guid Id { get; }

	string Name { get; }

	bool IsSignedIn { get; }

	/// <summary>The signed-in user, or null if not signed in or it couldn't be fetched.</summary>
	Task<AccountUser?> GetUserAsync();

	/// <summary>Asks the user to sign in; returns false if they cancelled or it failed.</summary>
	Task<bool> SignInAsync();

	void SignOut();
}

/// <param name="Avatar">URL of the user's picture, if they have one.</param>
public sealed record AccountUser(string Name, string? Avatar);

/// <summary>Sent (with WeakReferenceMessenger.Default) after the user signs in to or out of an account.</summary>
public sealed record AccountChangedMessage(Guid AccountId);
