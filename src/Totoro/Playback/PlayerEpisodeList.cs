using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Services;

namespace Totoro.Playback;

/// <summary>
/// The built-in player's episode list: gives it the anime's episodes (with their titles and thumbnails, where ani.zip has
/// them), and plays the one the user picks from it.
/// </summary>
internal sealed class PlayerEpisodeList(IPlaybackProgressStore progressStore, Action<AnimeModel, int> watch)
{
	// The built-in player whose episode list is followed (picking an episode plays it), and the anime it lists.
	private IInteractiveMediaPlayer? _listingPlayer;
	private AnimeModel? _listedAnime;

	// Episode details (titles, thumbnails) from ani.zip, by anime: fetched once while watching it.
	private readonly Dictionary<string, List<EpisodeInfo>> _episodeDetails = [];

	/// <summary>The episode's title, if its details were fetched already.</summary>
	public string? CachedTitle(AnimeModel anime, int number) =>
		_episodeDetails.TryGetValue(anime.StoreKey, out var cached) && cached.FirstOrDefault(x => x.EpisodeNumber == number) is { } info ? EpisodeTitle(info) : null;

	/// <summary>Stops following a list: an earlier anime's would play from its own provider.</summary>
	public void Forget() => _listedAnime = null;

	/// <summary>Lists the anime's episodes in the player, and says the playing one's title (to <paramref name="setEpisodeName"/>) once known.</summary>
	public async Task ListAsync(IInteractiveMediaPlayer interactive, AnimeModel anime, int current, Action<string> setEpisodeName, CancellationToken ct)
	{
		_listedAnime = anime;
		if (!ReferenceEquals(_listingPlayer, interactive))
		{
			_listingPlayer?.EpisodeRequested -= OnEpisodeRequested;
			interactive.EpisodeRequested += OnEpisodeRequested;
			_listingPlayer = interactive;
		}

		try
		{
			if (!_episodeDetails.TryGetValue(anime.StoreKey, out var details))
			{
				details = [.. (await anime.GetEpisodesAsync(ct)).Where(x => !x.IsSpecial)];
				if (details.Count > 0)
				{
					_episodeDetails[anime.StoreKey] = details;
				}
			}

			// The aired ones ani.zip knows of, or else as many as the anime has; the one playing at least.
			var count = Math.Max(current, details.Count > 0 ? details.Max(x => x.EpisodeNumber) : anime.TotalEpisodes ?? current);
			var watched = anime.Tracking?.WatchedEpisodes ?? 0;
			var saved = progressStore.GetAll(anime);
			List<PlayerEpisode> episodes = [];
			for (var number = 1; number <= count; number++)
			{
				var info = details.FirstOrDefault(x => x.EpisodeNumber == number);
				episodes.Add(new PlayerEpisode(
					number,
					info is null ? $"Episode {number}" : EpisodeTitle(info),
					info is null ? "" : EpisodeDetails(info),
					info?.Overview ?? "",
					string.IsNullOrEmpty(info?.Image) ? null : info.Image,
					number <= watched,
					number == current,
					saved.GetValueOrDefault(number)?.Fraction ?? 0));
			}

			if (ReferenceEquals(_listedAnime, anime))
			{
				if (details.FirstOrDefault(x => x.EpisodeNumber == current) is { } currentInfo)
				{
					setEpisodeName(EpisodeTitle(currentInfo));
				}

				interactive.SetEpisodes(episodes);
			}
		}
		catch (OperationCanceledException)
		{
			// Another episode was started meanwhile; it lists them.
		}
	}

	private void OnEpisodeRequested(object? sender, int number)
	{
		if (_listedAnime is { } anime)
		{
			watch(anime, number);
		}
	}

	private static string EpisodeTitle(EpisodeInfo info)
	{
		return new[] { info.Titles.English, info.Titles.Romaji, info.Titles.Japanese }.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
			   ?? $"Episode {info.EpisodeNumber}";
	}

	/// <summary>e.g. "3 Oct 2025 · 24 min".</summary>
	private static string EpisodeDetails(EpisodeInfo info)
	{
		List<string> details = [];
		if (info.AirDateUtc is { } aired)
		{
			details.Add(aired.ToLocalTime().ToString("d MMM yyyy"));
		}

		if (info.Runtime > 0)
		{
			details.Add($"{info.Runtime} min");
		}

		return string.Join(" · ", details);
	}
}
