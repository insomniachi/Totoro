using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Anime;

namespace Totoro.ViewModels.AnimeDetails;

/// <summary>A review, as the Reviews tab shows it: its summary, expanding to the review itself when there's more.</summary>
/// <param name="AuthorImage">Their avatar's URL; empty (never null, for x:Bind) when there's none.</param>
/// <param name="Meta">A line under the author, e.g. "★ 8.5 · 42 of 50 found it helpful · Mar 2025".</param>
/// <param name="Body">The review, in HTML; empty when there's no more than the summary.</param>
/// <param name="Url">Its page on the database; null when it has none.</param>
/// <param name="LinkText">The link to it, e.g. "Read on AniList".</param>
public sealed record ReviewItem(string Author, string AuthorImage, string Meta, string Summary, string Body, Uri? Url, string LinkText)
{
	/// <summary>Its accessible name.</summary>
	public string Name => $"Review by {Author}";

	/// <summary>Whether there's more than the summary to expand to: the review itself, or a link to it.</summary>
	public bool IsExpandable => Body.Length > 0 || Url is not null;

	/// <summary>What the shimmer's placeholder cards are built from.</summary>
	public static ReviewItem Placeholder { get; } = new("Reviewer", "", "★ 0.0 · 0 of 0 found it helpful", "A line summing the review up", "", null, "");

	public static ReviewItem From(ReviewModel review)
	{
		List<string?> meta =
		[
			review.Score is > 0 and var score ? $"★ {score / 10.0:0.0}" : null,
			review.Reception,
			review.Created?.LocalDateTime.ToString("MMM yyyy", CultureInfo.CurrentCulture),
		];

		// The databases' names as their sites write them.
		var site = review.Source switch
		{
			nameof(AnimeId.Anilist) => "AniList",
			"" => "the web",
			_ => review.Source,
		};

		// A deleted account's, say.
		var author = string.IsNullOrWhiteSpace(review.Author) ? "Anonymous" : review.Author;
		return new ReviewItem(author, review.AuthorImage ?? "", string.Join(" · ", meta.Where(x => !string.IsNullOrEmpty(x))),
							  review.Summary, review.Body, Uri.TryCreate(review.Url, UriKind.Absolute, out var url) ? url : null, $"Read on {site}");
	}
}

/// <summary>The anime's reviews (see <see cref="Core.Services.AnimeReviewService"/>).</summary>
public sealed partial class ReviewsTab(Func<Task<List<ReviewModel>>> fetch, ILogger logger)
	: LazyDetailsTab("No reviews yet.", logger)
{
	[ObservableProperty]
	public partial IReadOnlyList<ReviewItem> Reviews { get; private set; } = [];

	protected override async Task<int> LoadAsync()
	{
		Reviews = [.. (await fetch()).Select(ReviewItem.From)];
		return Reviews.Count;
	}
}
