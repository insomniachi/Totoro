using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Relations;
using Totoro.Core.Settings;

namespace Totoro.Playback;

/// <summary>
/// Asks an anime's provider (the one it's watched from) for its episodes, remembering what it has for a while. Used from
/// cards on the UI thread only, so it needs no locks.
/// </summary>
public sealed class ProviderEpisodes(WatchService watch, IAnimeSettingsStore animeSettings, IAnimeRelations relations,
									 ILogger<ProviderEpisodes> logger) : IProviderEpisodes
{
	// An episode that's aired may reach the provider later, so it's asked again after this.
	private static readonly TimeSpan s_recheckAfter = TimeSpan.FromMinutes(30);

	private readonly Dictionary<long, (int? Latest, DateTime At)> _known = [];
	private readonly Dictionary<long, Task<int?>> _checking = [];

	public void Check(AnimeModel anime) => _ = CheckAsync(anime);

	private async Task CheckAsync(AnimeModel anime)
	{
		var now = DateTime.Now;
		// Once the provider has every aired episode, there's nothing to ask until the next one airs.
		if (_known.TryGetValue(anime.Id, out var known) &&
			(now - known.At < s_recheckAfter || known.Latest >= EpisodeAvailability.AiredEpisodes(anime, now)))
		{
			anime.ProviderEpisodes = known.Latest;
			return;
		}

		// Every card showing the anime waits on the one request.
		if (!_checking.TryGetValue(anime.Id, out var check))
		{
			_checking[anime.Id] = check = GetLatestAsync(anime, CancellationToken.None);
		}

		try
		{
			var latest = await check;
			_known[anime.Id] = (latest, DateTime.Now);
			anime.ProviderEpisodes = latest;
		}
		finally
		{
			_checking.Remove(anime.Id);
		}
	}

	/// <summary>
	/// The latest of the anime's aired, unwatched episodes its provider has (the episodes watched when it has none of
	/// them); null when there's no provider, its show isn't matched, or it couldn't be reached.
	/// </summary>
	private async Task<int?> GetLatestAsync(AnimeModel anime, CancellationToken ct)
	{
		var watched = anime.Tracking?.WatchedEpisodes ?? 0;
		if (EpisodeAvailability.AiredEpisodes(anime, DateTime.Now) is not { } aired || watch.GetProvider(anime) is not { } provider)
		{
			return null;
		}

		try
		{
			// No picker: this runs unasked, so a show that isn't matched yet stays unknown.
			if ((await watch.FindShowAsync(provider, anime, ct)).Show is not { } show)
			{
				return null;
			}

			await relations.EnsureAvailableAsync(ct);
			var episodes = await provider.GetEpisodesAsync(show.Id, ct);
			var numbering = animeSettings.Get(anime).EpisodeNumbering;
			for (var number = aired; number > watched; number--)
			{
				if (WatchService.PickEpisode(episodes, anime, number, numbering, relations) is not null)
				{
					return number;
				}
			}

			return watched;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Couldn't check {Provider} for new episodes of {Anime}", provider.Name, anime.Title);
			return null;
		}
	}
}
