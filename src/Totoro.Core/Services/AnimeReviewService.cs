using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Services;

/// <summary>
/// An anime's reviews: its own database's, or AniList's (by the AniList id it maps to) when that has none or can't
/// load them, so every database shows some.
/// </summary>
public sealed class AnimeReviewService(IFactory<IAnimeMetadataProvider> databases, ILogger<AnimeReviewService> logger)
{
	/// <param name="database">The database <paramref name="anime"/> is from.</param>
	/// <exception cref="Exception">Its own reviews failed to load, and AniList's can't stand in.</exception>
	public async Task<List<ReviewModel>> GetReviewsAsync(IAnimeMetadataProvider database, AnimeModel anime)
	{
		if (database.Name == nameof(AnimeId.Anilist))
		{
			return await database.GetReviewsAsync(anime.Id);
		}

		ExceptionDispatchInfo? failure = null;
		try
		{
			if (await database.GetReviewsAsync(anime.Id) is { Count: > 0 } own)
			{
				return own;
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "{Database} couldn't load the reviews of {Anime}; showing AniList's", database.Name, anime.Title);
			failure = ExceptionDispatchInfo.Capture(ex);
		}

		// Resolved only now: CreateAll builds every database installed.
		if (databases.CreateAll().FirstOrDefault(x => x.Name == nameof(AnimeId.Anilist)) is { } anilist
			&& await WithSource(anime, database).FindIdAsync(nameof(AnimeId.Anilist)) is { } id)
		{
			return await anilist.GetReviewsAsync(id);
		}

		failure?.Throw();
		return [];
	}

	/// <summary>
	/// The anime, saying which database it's from, which looking up its AniList id needs: one passed on as given (when
	/// its details didn't load) may not.
	/// </summary>
	private static AnimeModel WithSource(AnimeModel anime, IAnimeMetadataProvider database) =>
		anime.ServiceName is { Length: > 0 } ? anime : new AnimeModel { Id = anime.Id, ServiceName = database.Name, ExternalIds = anime.ExternalIds };
}
