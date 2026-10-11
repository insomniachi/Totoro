using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Modules.Anime;

namespace Totoro.ViewModels.AnimeDetails;

// Keeps the model too, to open its details when clicked.
public sealed record RelatedItem(string Title, string? Image, AnimeModel Anime);

/// <summary>Related and recommended anime. They're part of the full details, so there's nothing to load lazily.</summary>
public sealed partial class RelatedTab : DetailsTab
{
	[ObservableProperty]
	public partial IReadOnlyList<RelatedItem> Related { get; private set; } = [];

	[ObservableProperty]
	public partial IReadOnlyList<RelatedItem> Recommended { get; private set; } = [];

	/// <summary>Fills the tab in from the anime's details.</summary>
	public void Show(AnimeModel details)
	{
		Related = [.. details.Related.Select(ToItem)];
		Recommended = [.. details.Recommended.Select(ToItem)];
		Message = Related.Count + Recommended.Count == 0 ? "Nothing related found." : null;
	}

	private static RelatedItem ToItem(AnimeModel anime) => new(anime.Title, NullIfEmpty(anime.Image), anime);
}
