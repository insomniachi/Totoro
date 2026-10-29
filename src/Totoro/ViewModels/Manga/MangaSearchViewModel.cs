using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;
using Totoro.Core.Navigation;
using Totoro.Core.Services;
using Totoro.Core.Settings;

namespace Totoro.ViewModels.Manga;

/// <summary>Manga mode's Search: as the anime one, without seasons (see <see cref="MediaSearchViewModel"/>).</summary>
/// <remarks>
/// Its fields are initialized from a primary constructor's parameters: initializers run before the base constructor,
/// which selects a database and so already loads its genres.
/// </remarks>
public sealed class MangaSearchViewModel(
	IFactory<IMangaMetadataProvider, Guid> providers,
	IFactory<IMangaTracker, Guid> trackers,
	IPreferences preferences,
	IEnumerable<Descriptor> modules,
	ILogger<MangaSearchViewModel> logger)
	: MediaSearchViewModel(MediaMode.Manga,
		[.. providers.CreateAll().Select(x => new AnimeDatabaseOption(x.Id, x.Name, modules.FirstOrDefault(m => m.Id == x.Id)?.Image ?? ""))],
		preferences.SelectedMangaDatabase, logger), IRecipient<MangaTrackingChangedMessage>
{
	private readonly IFactory<IMangaMetadataProvider, Guid> _providers = providers;
	private readonly IFactory<IMangaTracker, Guid> _trackers = trackers;
	private Task<List<MangaModel>>? _userList;

	// A primary constructor has no body: changes elsewhere are followed from arriving on the page.
	protected override Task OnNavigatedToInternalAsync()
	{
		WeakReferenceMessenger.Default.RegisterAll(this);
		return Task.CompletedTask;
	}

	protected override async Task<List<string>> GetGenresAsync(Guid provider) =>
		_providers.Create(provider) is { } database ? await database.GetGenresAsync() : [];

	protected override async Task<IReadOnlyList<IMediaModel>> SearchAsync(Guid provider, AdvancedSearchRequest request, CancellationToken ct)
	{
		if (_providers.Create(provider) is not { } database)
		{
			return [];
		}

		var results = await database.SearchMangaAsync(request, ct);
		await AddListEntriesAsync(results, provider);
		return results;
	}

	/// <summary>
	/// Gives results without their list entry (MAL's filtered search goes through Jikan, which doesn't know the user)
	/// the user's, from their list on that database; fetched once.
	/// </summary>
	private async Task AddListEntriesAsync(List<MangaModel> results, Guid provider)
	{
		if (results.All(x => x.Tracking is not null) || _trackers.Create(provider) is not { } tracker || tracker.Id != provider)
		{
			return;
		}

		try
		{
			var list = (await (_userList ??= tracker.GetUserListAsync(CancellationToken.None))).ToDictionary(x => x.Id);
			foreach (var manga in results.Where(x => x.Tracking is null))
			{
				manga.Tracking = list.GetValueOrDefault(manga.Id)?.Tracking;
			}
		}
		catch (Exception)
		{
			// Signed out, or offline: the results show without their place on the list.
			_userList = null;
		}
	}

	protected override Task OnNavigatedFromInternalAsync()
	{
		WeakReferenceMessenger.Default.Unregister<MangaTrackingChangedMessage>(this);
		return base.OnNavigatedFromInternalAsync();
	}

	/// <summary>A manga's list entry changed through another instance of it (e.g. by an agent tool): its card shows it.</summary>
	public void Receive(MangaTrackingChangedMessage message)
	{
		foreach (var manga in Results.OfType<MangaModel>().Where(x => !ReferenceEquals(x, message.Manga) && x.Id == message.Manga.Id && x.ServiceId == message.Manga.ServiceId))
		{
			manga.Tracking = message.Tracking;
		}
	}
}
