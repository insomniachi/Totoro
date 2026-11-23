using Totoro.Core.Agent;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;

namespace Totoro.Agent;

/// <summary>Finds anime for agent tools: by their ids in the selected anime database, or by the title the user wrote.</summary>
public sealed class AnimeLookup(IFactory<IAnimeMetadataProvider> providers, IFactory<IAnimeTracker> trackers)
{
	// For tools that take an anime by title or id (GetAsync's two parameters).
	public const string TitleHelp = "The anime's title, as the user wrote it; if it could be several, the user picks.";
	public const string IdHelp = "Its id, only if a tool gave it.";

	/// <summary>The selected anime database.</summary>
	public IAnimeMetadataProvider Database => providers.CreateDefault() ?? throw new AgentToolException("No anime database is set up.");

	/// <summary>The anime, with the user's list entry for it.</summary>
	public async Task<AnimeModel> GetAsync(long animeId)
	{
		var database = Database;
		try
		{
			return await database.GetAnimeAsync(animeId);
		}
		catch (Exception ex)
		{
			throw new AgentToolException($"Couldn't get anime {animeId} from {database.Name} ({ex.Message}). Search for it to get its id.");
		}
	}

	/// <summary>
	/// The anime by its id, or else by its title. A title that could be several anime (a franchise, a typo) throws
	/// <see cref="AgentChoicesException"/>, for the user to pick.
	/// </summary>
	public async Task<AnimeModel> GetAsync(long? animeId, string? title)
	{
		if (animeId is { } id)
		{
			return await GetAsync(id);
		}

		if (string.IsNullOrWhiteSpace(title))
		{
			throw new AgentToolException("Give the anime's title, as the user wrote it.");
		}

		var found = await Database.FindAnimeAsync(new AdvancedSearchRequest { Title = title.Trim() }, trackers.CreateDefault());
		if (found.Count == 0)
		{
			throw new AgentToolException($"No anime matches \"{title}\". If it's misspelt, try again with it spelt right; else tell the user it wasn't found.");
		}

		// Only a title that is the best match's (e.g. "durarara" for "Durarara!!") is sure; a franchise ("fate") or a typo,
		// the user picks.
		if (found.Count > 1 && !IsTitleOf(found[0], title))
		{
			throw new AgentChoicesException(AnimePick.Of(found.Take(5)));
		}

		// In full, as by id: search results leave out details (related, recommended...).
		return await GetAsync(found[0].Id);
	}

	/// <summary>Whether <paramref name="title"/> is one of the anime's titles, but for case, spaces and punctuation.</summary>
	internal static bool IsTitleOf(AnimeModel anime, string title)
	{
		static string Plain(string? text) => new([.. (text ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);
		var wanted = Plain(title);
		return wanted.Length > 0 && new[] { anime.Title, anime.EngTitle, anime.RomajiTitle }.Concat(anime.AlternateTitles).Any(x => Plain(x) == wanted);
	}
}
