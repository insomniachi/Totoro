using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Navigation;
using Totoro.Core.Services;
using Totoro.Core.Settings;

namespace Totoro.ViewModels;

/// <summary>The anime Search page: an anime database searched by title, season, years, score and genres (see <see cref="MediaSearchViewModel"/>).</summary>
/// <remarks>
/// Its fields are initialized from a primary constructor's parameters: initializers run before the base constructor,
/// which selects a database and so already loads its genres.
/// </remarks>
public sealed class AdvancedSearchViewModel(
	IFactory<IAnimeMetadataProvider, Guid> providers,
	IFactory<IAnimeTracker, Guid> trackers,
	IPreferences preferences,
	IEnumerable<Descriptor> modules,
	ILogger<AdvancedSearchViewModel> logger)
	: MediaSearchViewModel(MediaMode.Anime,
		[.. providers.CreateAll().Select(x => new AnimeDatabaseOption(x.Id, x.Name, modules.FirstOrDefault(m => m.Id == x.Id)?.Image ?? ""))],
		preferences.SelectedAnimeDatabase, logger), IRecipient<AnimeTrackingChangedMessage>
{
	private readonly IFactory<IAnimeMetadataProvider, Guid> _providers = providers;
	private readonly IFactory<IAnimeTracker, Guid> _trackers = trackers;

	// A primary constructor has no body: changes elsewhere are followed from arriving on the page.
	protected override Task OnNavigatedToInternalAsync()
	{
		WeakReferenceMessenger.Default.RegisterAll(this);
		return Task.CompletedTask;
	}

	protected override async Task<List<string>> GetGenresAsync(Guid provider) =>
		_providers.Create(provider) is { } database ? await database.GetGenresAsync() : [];

	protected override async Task<IReadOnlyList<IMediaModel>> SearchAsync(Guid provider, AdvancedSearchRequest request, CancellationToken ct) =>
		_providers.Create(provider) is { } database ? await database.FindAnimeAsync(request, _trackers.CreateDefault(), ct) : [];

	protected override Task OnNavigatedFromInternalAsync()
	{
		WeakReferenceMessenger.Default.Unregister<AnimeTrackingChangedMessage>(this);
		return base.OnNavigatedFromInternalAsync();
	}

	/// <summary>An anime's list entry changed through another instance of it (e.g. by an agent tool): its card shows it.</summary>
	public void Receive(AnimeTrackingChangedMessage message)
	{
		foreach (var anime in Results.OfType<AnimeModel>().Where(x => !ReferenceEquals(x, message.Anime) && x.StoreKey == message.Anime.StoreKey))
		{
			anime.Tracking = message.Tracking;
		}
	}
}
