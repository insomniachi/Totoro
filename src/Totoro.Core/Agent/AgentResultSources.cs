using Microsoft.Extensions.AI;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;

namespace Totoro.Core.Agent;

// What a tool's result is made from, beyond the JSON the agent sees: a chat shows it (anime as cards, songs with their
// player). Kept out of the JSON (explicit interface members aren't serialized).

/// <summary>A tool result about an anime, e.g. a search result.</summary>
public interface IAgentAnime
{
	AnimeModel? Source { get; }
}

/// <summary>A tool result of anime in an order that matters, e.g. the user's list by episodes: shown in that order.</summary>
public interface IAgentAnimeList
{
	IReadOnlyList<IAgentAnime> Ranked { get; }

	/// <summary>How many there are in all, of which <see cref="Ranked"/> are the first.</summary>
	int Total { get; }
}

/// <summary>A tool result about an anime's songs, e.g. the one playing.</summary>
public interface IAgentSongs
{
	AnimeModel? Anime { get; }

	IReadOnlyList<AnimeSong> Songs { get; }
}

/// <summary>A tool's result as it returned it, before it was made JSON for the agent.</summary>
public static class AgentToolResult
{
	private static readonly object s_key = new();

	/// <summary>
	/// What the tool called with <paramref name="arguments"/> returned (e.g. its <see cref="IAgentAnime"/>s); null if it
	/// returned nothing, or hasn't been called with them.
	/// </summary>
	public static object? Of(AIFunctionArguments arguments) => arguments.Context?.TryGetValue(s_key, out var value) == true ? value : null;

	internal static void Set(AIFunctionArguments arguments, object? value)
	{
		arguments.Context ??= new Dictionary<object, object?>();
		arguments.Context[s_key] = value;
	}
}
