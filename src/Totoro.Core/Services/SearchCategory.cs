namespace Totoro.Core.Services;

/// <summary>What the title bar's search looks for: one kind at a time.</summary>
public enum SearchCategory
{
	Anime,
	Manga,
	Character,
	VoiceActor,
	Song,
}

public static class SearchCategories
{
	public static IReadOnlyList<SearchCategory> All { get; } = Enum.GetValues<SearchCategory>();

	/// <summary>What's typed before the search to pick the category, e.g. "c:" for characters.</summary>
	public static string Prefix(SearchCategory category) => category switch
	{
		SearchCategory.Anime => "a:",
		SearchCategory.Manga => "m:",
		SearchCategory.Character => "c:",
		SearchCategory.VoiceActor => "v:",
		_ => "s:",
	};

	/// <summary>e.g. "Voice actors", for the category menu and the box's placeholder.</summary>
	public static string Name(SearchCategory category) => category switch
	{
		SearchCategory.Anime => "Anime",
		SearchCategory.Manga => "Manga",
		SearchCategory.Character => "Characters",
		SearchCategory.VoiceActor => "Voice actors",
		_ => "Songs",
	};

	/// <summary>
	/// The category <paramref name="text"/> starts with a prefix of (e.g. "v: kana"), and what's searched for after it;
	/// no category, and the text as typed, without one.
	/// </summary>
	public static (SearchCategory? Category, string Query) Parse(string text)
	{
		var trimmed = text.TrimStart();
		foreach (var category in All)
		{
			if (trimmed.StartsWith(Prefix(category), StringComparison.OrdinalIgnoreCase))
			{
				return (category, trimmed[Prefix(category).Length..].Trim());
			}
		}

		return (null, text.Trim());
	}
}

/// <summary>
/// A row of the title bar's search, whatever its category: a picture, a title, a line of facts, where it is on the
/// user's list (anime and manga), and what opens in the side pane when it's picked.
/// </summary>
/// <param name="Data">What the side pane navigates to: an anime, manga, character, voice actor or song.</param>
public sealed record SearchSuggestion(string Title, string? Image, string Facts, string ListStatus, string Tooltip, object Data)
{
	public static SearchSuggestion From(MediaSuggestion media) =>
		new(media.Media.Title, media.Media.Image, media.Facts, media.ListStatus, media.FullTitle, media.Media);
}
