using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GraphQL.Client.Http;
using Totoro.Core.Modules;
using Totoro.Core.Services;

namespace Totoro.ViewModels;

/// <summary>
/// Why a page showing the user's list has nothing to show: they aren't signed in to the selected database, or loading
/// failed. Shown by <c>ListLoadInfoBars</c>, which offer to sign in and to retry.
/// </summary>
/// <param name="load">Loads the page's list; what it throws is shown as <see cref="Error"/>.</param>
/// <param name="purpose">What the list is for, to finish "Sign in to AniList ...", e.g. "to see your anime list".</param>
public sealed partial class ListLoadState(AnimeTrackingService tracking, Func<Task> load, string purpose) : ObservableObject
{
	private IAccount? _signedOut;

	/// <summary>"" while signed in.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasMessage))]
	public partial string SignInMessage { get; set; } = "";

	/// <summary>Why loading failed; "" if it didn't.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasMessage))]
	public partial string Error { get; set; } = "";

	/// <summary>Whether there's something to say; the InfoBars take no room otherwise.</summary>
	public bool HasMessage => SignInMessage.Length > 0 || Error.Length > 0;

	public async Task LoadAsync()
	{
		_signedOut = tracking.GetSignedOutListAccount();
		SignInMessage = _signedOut is { } account ? $"Sign in to {account.Name} {purpose}." : "";
		Error = "";
		try
		{
			await load();
		}
		catch (Exception ex) when (ex is HttpRequestException or GraphQLHttpRequestException)
		{
			Error = $"Couldn't reach the anime database; it may be down, or you may be offline. Try again in a bit. ({ex.Message})";
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Error = $"Couldn't load your list: {ex.Message}";
		}
	}

	[RelayCommand]
	private Task RetryAsync() => LoadAsync();

	[RelayCommand]
	private async Task SignInAsync()
	{
		if (_signedOut is { } account && await account.SignInAsync())
		{
			await LoadAsync();
		}
	}
}
