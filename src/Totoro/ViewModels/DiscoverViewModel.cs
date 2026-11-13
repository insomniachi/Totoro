using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Helpers;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>
/// Anime to pick something new to watch from, from the selected database: what's trending (the first few in a
/// spotlight), what airs an episode today, what starts next season, and sequels and spin-offs of what the user watched
/// that they missed (the first few; See all has the rest). The agent gets the same lists with discover_anime.
/// </summary>
public sealed partial class DiscoverViewModel(
	IFactory<IAnimeMetadataProvider, Guid> providers,
	MissedRelationsService missedRelations,
	ILogger<DiscoverViewModel> logger)
	: NavigatableViewModel, IRecipient<AnimeTrackingChangedMessage>, Core.Navigation.IAnimeDatabaseView
{
	private const int SpotlightCount = 8;
	private const int MissedCount = 20;

	[ObservableProperty] public partial IReadOnlyList<AnimeModel> Spotlight { get; private set; } = [];
	[ObservableProperty] public partial IReadOnlyList<AnimeModel> Trending { get; private set; } = [];
	[ObservableProperty] public partial IReadOnlyList<AnimeModel> AiringToday { get; private set; } = [];
	[ObservableProperty] public partial IReadOnlyList<AnimeModel> NextSeason { get; private set; } = [];
	[ObservableProperty] public partial IReadOnlyList<AnimeModel> Missed { get; private set; } = [];
	// Missed comes in well after the rest of the page, so it has its own placeholder row.
	[ObservableProperty] public partial bool IsLoadingMissed { get; private set; }

	private static Season Next => Season.Of(DateTime.Now.AddMonths(3));

	public string NextSeasonTitle => $"Next season ({Next.SeasonName} {Next.Year})";

	/// <summary>Why nothing loaded (every row failed); "" if anything did.</summary>
	[ObservableProperty] public partial string Error { get; private set; } = "";

	protected override Task OnNavigatedToInternalAsync()
	{
		WeakReferenceMessenger.Default.Register(this);
		return LoadAsync();
	}

	[RelayCommand]
	private Task RetryAsync() => RunBusyAsync(LoadAsync);

	protected override Task OnNetworkRestoredAsync() => Error.Length > 0 ? LoadAsync() : Task.CompletedTask;

	private async Task LoadAsync()
	{
		Error = "";
		if (providers.CreateDefault() is not { } database)
		{
			return;
		}

		// All at once; each comes back empty if its request fails (the providers log it).
		Exception? failure = null;
		var trending = LoadRowAsync(database.GetPopularAnimeAsync, ex => failure ??= ex);
		var today = LoadRowAsync(database.GetAiringTodayAsync, ex => failure ??= ex);
		var next = LoadRowAsync(database.GetUpcomingAnimeAsync, ex => failure ??= ex);
		// Its own time (a request per 50 anime watched): its row shows up when it's ready, without holding up the page.
		_ = LoadMissedAsync();
		Trending = await trending;
		Spotlight = [.. Trending.Where(x => !string.IsNullOrEmpty(x.BannerImage)).Take(SpotlightCount)];
		AiringToday = await today;
		NextSeason = await next;
		// One empty row is just an empty row; all of them, with a failure, is the database not answering.
		if (failure is not null && Trending.Count == 0 && AiringToday.Count == 0 && NextSeason.Count == 0)
		{
			Error = NetworkErrors.Describe(failure, database.Name);
		}
	}

	private async Task<IReadOnlyList<AnimeModel>> LoadRowAsync(Func<CancellationToken, Task<List<AnimeModel>>> load, Action<Exception> failed)
	{
		try
		{
			return await load(CancellationToken.None);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't load anime to discover");
			failed(ex);
			return [];
		}
	}

	private async Task LoadMissedAsync()
	{
		IsLoadingMissed = true;
		try
		{
			Missed = [.. (await missedRelations.GetAsync()).Take(MissedCount)];
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't load missed sequels and spin-offs");
		}
		finally
		{
			IsLoadingMissed = false;
		}
	}

	[RelayCommand]
	private Task SeeAllMissedAsync()
	{
		return Navigators.TryGetNavigator("main", out var main) ? main.NavigateToViewModelAsync(typeof(MissedRelationsViewModel)) : Task.CompletedTask;
	}

	protected override Task OnNavigatedFromInternalAsync()
	{
		WeakReferenceMessenger.Default.Unregister<AnimeTrackingChangedMessage>(this);
		return Task.CompletedTask;
	}

	/// <summary>An anime's list entry changed through another instance of it (e.g. by an agent tool): its card shows it.</summary>
	public void Receive(AnimeTrackingChangedMessage message)
	{
		foreach (var anime in Trending.Concat(AiringToday).Concat(NextSeason).Concat(Missed)
									  .Where(x => !ReferenceEquals(x, message.Anime) && x.StoreKey == message.Anime.StoreKey))
		{
			anime.Tracking = message.Tracking;
		}
	}
}
