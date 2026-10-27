using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>
/// Anime to pick something new to watch from, from the selected database: what's trending (the first few in a
/// spotlight), what airs an episode today, and what starts next season. The agent gets the same lists
/// with discover_anime.
/// </summary>
public sealed partial class DiscoverViewModel(IFactory<IAnimeMetadataProvider, Guid> providers, ILogger<DiscoverViewModel> logger)
	: NavigatableViewModel, IRecipient<AnimeTrackingChangedMessage>
{
	private const int SpotlightCount = 8;

	[ObservableProperty] public partial IReadOnlyList<AnimeModel> Spotlight { get; private set; } = [];
	[ObservableProperty] public partial IReadOnlyList<AnimeModel> Trending { get; private set; } = [];
	[ObservableProperty] public partial IReadOnlyList<AnimeModel> AiringToday { get; private set; } = [];
	[ObservableProperty] public partial IReadOnlyList<AnimeModel> NextSeason { get; private set; } = [];

	private static Season Next => Season.Of(DateTime.Now.AddMonths(3));

	public string NextSeasonTitle => $"Next season ({Next.SeasonName} {Next.Year})";

	protected override async Task OnNavigatedToInternalAsync()
	{
		WeakReferenceMessenger.Default.Register(this);
		if (providers.CreateDefault() is not { } database)
		{
			return;
		}

		// All at once; each comes back empty if its request fails (the providers log it).
		var trending = LoadAsync(database.GetPopularAnimeAsync);
		var today = LoadAsync(database.GetAiringTodayAsync);
		var next = LoadAsync(database.GetUpcomingAnimeAsync);
		Trending = await trending;
		Spotlight = [.. Trending.Where(x => !string.IsNullOrEmpty(x.BannerImage)).Take(SpotlightCount)];
		AiringToday = await today;
		NextSeason = await next;
	}

	private async Task<IReadOnlyList<AnimeModel>> LoadAsync(Func<CancellationToken, Task<List<AnimeModel>>> load)
	{
		try
		{
			return await load(CancellationToken.None);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't load anime to discover");
			return [];
		}
	}

	protected override Task OnNavigatedFromInternalAsync()
	{
		WeakReferenceMessenger.Default.Unregister<AnimeTrackingChangedMessage>(this);
		return Task.CompletedTask;
	}

	/// <summary>An anime's list entry changed through another instance of it (e.g. by an agent tool): its card shows it.</summary>
	public void Receive(AnimeTrackingChangedMessage message)
	{
		foreach (var anime in Trending.Concat(AiringToday).Concat(NextSeason)
									  .Where(x => !ReferenceEquals(x, message.Anime) && x.StoreKey == message.Anime.StoreKey))
		{
			anime.Tracking = message.Tracking;
		}
	}
}
