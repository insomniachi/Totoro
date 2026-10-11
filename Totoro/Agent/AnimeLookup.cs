using Totoro.Core.Agent;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;

namespace Totoro.Agent;

/// <summary>Finds anime by the ids agent tools are given: ids in the selected anime database.</summary>
public sealed class AnimeLookup(IFactory<IAnimeMetadataProvider, Guid> providers)
{
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
}
