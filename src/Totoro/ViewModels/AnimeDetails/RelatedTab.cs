using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Anime;

namespace Totoro.ViewModels.AnimeDetails;

// Keeps the model too, to open its details when clicked.
public sealed record RelatedItem(string Title, string? Image, AnimeModel Anime)
{
	/// <summary>What the shimmer's placeholder cards are built from.</summary>
	public static RelatedItem Placeholder { get; } = new("A title of usual length", null, new AnimeModel());
}

/// <summary>
/// The whole series the anime belongs to, in the order it aired (fetched, entry by entry), then its other related anime
/// (side stories, spin-offs...) and recommendations, which come with the details.
/// </summary>
public sealed partial class RelatedTab(Func<AnimeModel> details, Func<AnimeModel, Task<IReadOnlyList<AnimeModel>>> fetchSeries, ILogger logger)
	: LazyDetailsTab("Nothing related found.", logger)
{
	[ObservableProperty]
	public partial IReadOnlyList<RelatedItem> Series { get; private set; } = [];

	[ObservableProperty]
	public partial IReadOnlyList<RelatedItem> Related { get; private set; } = [];

	[ObservableProperty]
	public partial IReadOnlyList<RelatedItem> Recommended { get; private set; } = [];

	protected override async Task<int> LoadAsync()
	{
		var anime = details();
		// Shown before the series loads, so they stay if it fails.
		Related = [.. anime.Related.Select(ToItem)];
		Recommended = [.. anime.Recommended.Select(ToItem)];

		var series = await fetchSeries(anime);
		if (series.Count > 1)
		{
			var inSeries = series.Select(x => x.Id).ToHashSet();
			Series = [.. series.Select(ToItem)];
			Related = [.. anime.Related.Where(x => !inSeries.Contains(x.Id)).Select(ToItem)];
		}

		return Series.Count + Related.Count + Recommended.Count;
	}

	private static RelatedItem ToItem(AnimeModel anime) => new(anime.Title, NullIfEmpty(anime.Image), anime);
}
