namespace Totoro.Core.Modules.Anime;

/// <summary>A user's review of an anime, or a database's shorter take on one (e.g. Kitsu's reactions).</summary>
/// <param name="Source">The database it's from, e.g. "Anilist".</param>
/// <param name="AuthorImage">Their avatar's URL; null when they have none.</param>
/// <param name="Summary">A line summing it up (the reaction itself, for a short one).</param>
/// <param name="Body">The review, in HTML (like <see cref="AnimeModel.Description"/>); "" when there's no more than <paramref name="Summary"/>.</param>
/// <param name="Score">The score it gives, out of 100.</param>
/// <param name="Reception">How readers took it, in the database's own terms, e.g. "42 of 50 found it helpful" or "98 upvotes".</param>
/// <param name="Url">Its page on the database; null when it has none.</param>
public sealed record ReviewModel(
	string Source,
	string Author,
	string? AuthorImage,
	string Summary,
	string Body,
	int? Score,
	string? Reception,
	string? Url,
	DateTimeOffset? Created);
