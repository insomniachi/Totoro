using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Navigation;
using Totoro.Core.Services;
using Totoro.Core.Settings;

namespace Totoro.ViewModels;

/// <summary>The anime Library: the user's anime list (see <see cref="MediaListViewModel"/>).</summary>
public sealed class UserListViewModel(IFactory<IAnimeTracker, Guid> trackers, AnimeTrackingService tracking, IPreferences preferences)
	: MediaListViewModel(MediaMode.Anime, preferences, tracking.GetSignedOutListAccount), IRecipient<AnimeTrackingChangedMessage>
{
	protected override async Task<(Guid TrackerId, IReadOnlyList<IMediaModel> Items)?> GetListAsync()
	{
		if (trackers.CreateDefault() is not { } tracker)
		{
			return null;
		}

		if (!WeakReferenceMessenger.Default.IsRegistered<AnimeTrackingChangedMessage>(this))
		{
			WeakReferenceMessenger.Default.Register(this);
		}

		return (tracker.Id, await tracker.GetUserListAsync(CancellationToken.None));
	}

	protected override Task OnNavigatedFromInternalAsync()
	{
		WeakReferenceMessenger.Default.Unregister<AnimeTrackingChangedMessage>(this);
		return Task.CompletedTask;
	}

	public void Receive(AnimeTrackingChangedMessage message) =>
		OnListChanged(message.Anime, message.Tracking is null, listed => listed.Tracking = message.Tracking);
}
