using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Totoro.Core.Services;

/// <summary>
/// The anime ids (and titles) the assistant's model has been given in a conversation, by tools or the user, oldest first:
/// the ones it may use. Small models garble long numbers they copy (135865 as 103574), and a made-up id can still be
/// some other anime, so a call with any other is turned away. Used from tool calls, so it's thread-safe.
/// </summary>
internal sealed class KnownAnimeIds
{
	private readonly List<(long Id, string Title)> _known = [];

	/// <summary>Forgets them all, for a new conversation.</summary>
	public void Clear()
	{
		lock (_known)
		{
			_known.Clear();
		}
	}

	/// <summary>
	/// Why an anime id the model passed is wrong, if it is: one it hasn't been given in this conversation. The error lists
	/// the ones it has been given, to pick from.
	/// </summary>
	public string? MadeUp(AIFunctionArguments arguments)
	{
		foreach (var (name, value) in arguments)
		{
			if (!name.EndsWith("animeId", StringComparison.OrdinalIgnoreCase) || IdOf(value) is not { } id)
			{
				continue;
			}

			lock (_known)
			{
				if (_known.Any(x => x.Id == id))
				{
					continue;
				}

				var known = string.Join(", ", _known.AsEnumerable().Reverse().DistinctBy(x => x.Id).Take(8).Select(x => $"{x.Title} is {x.Id}"));
				return known.Length == 0
					? $"No tool has given you anime id {id}, so it's wrong. Search for the anime to get its id."
					: $"No tool has given you anime id {id} in this conversation, so it's wrong: copy one they gave you exactly. Latest first: {known}. Or search for the anime to get its id.";
			}
		}

		return null;

		static long? IdOf(object? value) => value switch
		{
			long x => x,
			int x => x,
			JsonElement { ValueKind: JsonValueKind.Number } x when x.TryGetInt64(out var id) => id,
			JsonElement { ValueKind: JsonValueKind.String } x when long.TryParse(x.GetString(), out var id) => id,
			string x when long.TryParse(x, out var id) => id,
			_ => null,
		};
	}

	/// <summary>Numbers in the user's message are ids they gave, which the model may use too.</summary>
	public void RememberUserIds(string text)
	{
		lock (_known)
		{
			foreach (var number in Regex.Matches(text, @"\b\d{3,}\b").Select(x => long.Parse(x.Value, CultureInfo.InvariantCulture)))
			{
				_known.Add((number, "the id you were given"));
			}
		}
	}

	/// <summary>Remembers the anime ids in a tool's result (with their titles), as ones the model may use.</summary>
	public void RememberResult(object? result)
	{
		if (result is not JsonElement json)
		{
			return;
		}

		lock (_known)
		{
			Walk(json);
		}

		void Walk(JsonElement element)
		{
			if (element.ValueKind == JsonValueKind.Array)
			{
				foreach (var item in element.EnumerateArray())
				{
					Walk(item);
				}

				return;
			}

			if (element.ValueKind != JsonValueKind.Object)
			{
				return;
			}

			var title = element.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
			foreach (var property in element.EnumerateObject())
			{
				if ((property.NameEquals("id") || property.Name.EndsWith("animeId", StringComparison.OrdinalIgnoreCase)) &&
					property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out var id))
				{
					_known.Add((id, title));
				}
				else
				{
					Walk(property.Value);
				}
			}
		}
	}
}
