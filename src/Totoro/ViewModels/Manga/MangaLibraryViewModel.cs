using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Manga;
using Totoro.Core.Navigation;
using Totoro.Core.Services;
using Totoro.Core.Settings;

namespace Totoro.ViewModels.Manga;

/// <summary>Manga mode's Library: the user's manga list, as the anime one (see <see cref="MediaListViewModel"/>).</summary>
public sealed class MangaLibraryViewModel(IFactory<IMangaTracker, Guid> trackers, IEnumerable<IAccount> accounts, IPreferences preferences)
	: MediaListViewModel(MediaMode.Manga, preferences, () => SignedOutAccount(trackers, accounts)),
	  IRecipient<MangaTrackingChangedMessage>, IRecipient<MangaDatabaseChangedMessage>
{
	// Manga lists aren't kept on this PC: without an account there's none.
	private static IAccount? SignedOutAccount(IFactory<IMangaTracker, Guid> trackers, IEnumerable<IAccount> accounts) =>
		trackers.CreateDefault() is { } tracker ? accounts.FirstOrDefault(x => x.Id == tracker.Id && !x.IsSignedIn) : null;

	protected override async Task<(Guid TrackerId, IReadOnlyList<IMediaModel> Items)?> GetListAsync()
	{
		if (trackers.CreateDefault() is not { } tracker)
		{
			return null;
		}

		if (!WeakReferenceMessenger.Default.IsRegistered<MangaTrackingChangedMessage>(this))
		{
			WeakReferenceMessenger.Default.RegisterAll(this);
		}

		return (tracker.Id, await tracker.GetUserListAsync(CancellationToken.None));
	}

	protected override Task OnNavigatedFromInternalAsync()
	{
		WeakReferenceMessenger.Default.UnregisterAll(this);
		return Task.CompletedTask;
	}

	public void Receive(MangaTrackingChangedMessage message) =>
		OnListChanged(message.Manga, message.Tracking is null, listed => listed.Tracking = message.Tracking);

	public void Receive(MangaDatabaseChangedMessage message) => _ = LoadState.LoadAsync();
}
