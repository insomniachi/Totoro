using System.ComponentModel;
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

/// <summary>A tool result about songs, e.g. an anime's, or the one playing: each with the anime it's from.</summary>
public interface IAgentSongs
{
	IReadOnlyList<SongMatch> Shown { get; }
}

/// <summary>A tool result about characters, e.g. an anime's cast: shown as a grid of their pictures and names.</summary>
public interface IAgentCharacters
{
	IReadOnlyList<CharacterModel> Shown { get; }
}

/// <summary>
/// A tool result asking the user to pick, e.g. which of several anime they meant: shown as buttons, each sending its
/// <see cref="AgentChoice.Message"/> as theirs, so they needn't type.
/// </summary>
public interface IAgentChoices
{
	string Question { get; }

	IReadOnlyList<AgentChoice> Choices { get; }

	/// <summary>Whether a choice's message only completes the user's request (e.g. "I mean X"), so it's sent with it.</summary>
	bool CompletesRequest => false;
}

/// <summary>
/// The anime a title given to a tool could be, for the user to pick: the tool throws <see cref="AgentChoicesException"/>
/// with it, and the pick, sent with their request, names the anime by id.
/// </summary>
public sealed record AnimePick(
	[property: Description("The anime the title could be (with their ids), shown to the user as buttons to pick one: stop and say nothing. Each time the user asks, call the tool again with the title: the buttons are only shown then.")]
	IReadOnlyList<string> PickOne) : IAgentChoices
{
	private IReadOnlyList<AgentChoice> _choices = [];

	string IAgentChoices.Question => "Which anime did you mean?";

	IReadOnlyList<AgentChoice> IAgentChoices.Choices => _choices;

	bool IAgentChoices.CompletesRequest => true;

	public static AnimePick Of(IEnumerable<AnimeModel> candidates)
	{
		var anime = candidates.ToList();
		return new AnimePick([.. anime.Select(x => $"{x.Title} (anime id {x.Id})")])
		{
			_choices = [.. anime.Select(x => new AgentChoice(x.Title, $"I mean {x.Title}, anime id {x.Id}", x))],
		};
	}
}

/// <summary>Thrown by a tool when it can't tell which anime it was asked about: the user picks (<see cref="AnimePick"/>).</summary>
public sealed class AgentChoicesException(AnimePick pick) : Exception("The user is picking which anime they meant.")
{
	public AnimePick Pick { get; } = pick;
}

/// <summary>One of an <see cref="IAgentChoices"/>' choices; <paramref name="Anime"/> is shown with it, if it's one.</summary>
public sealed record AgentChoice(string Label, string Message, AnimeModel? Anime = null);

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
