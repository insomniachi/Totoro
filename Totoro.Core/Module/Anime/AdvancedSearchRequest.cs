namespace Totoro.Core.Module.Anime;

public class AdvancedSearchRequest
{
	public string? Title { get; init; }
	public AnimeSeason? SeasonName { get; init; }
	public AnimeSource? Source { get; init; }
	public List<string>? IncludedGenres { get; init; }
	public List<string>? ExcludedGenres { get; init; }
	public float? MinimumScore { get; init; }
	public float? MaximumScore { get; init; }
	public int? MaxYear { get; init; }
	public int? MinYear { get; init; }

	public bool IsEmpty()
	{
		return string.IsNullOrEmpty(Title) &&
			   SeasonName == null &&
			   Source == null &&
			   (IncludedGenres == null || IncludedGenres.Count == 0) &&
			   (ExcludedGenres == null || ExcludedGenres.Count == 0) &&
			   MinimumScore == null &&
			   MaximumScore == null &&
			   MinYear == null &&
			   MaxYear == null;
	}
}

// Same order as AniList's MediaSource, so the AniList provider can cast between them.
public enum AnimeSource
{
	Original,
	Manga,
	LightNovel,
	VisualNovel,
	VideoGame,
	Other,
	Novel,
	Doujinshi,
	Anime,
	WebNovel,
	LiveAction,
	Game,
	Comic,
	MultimediaProject,
	PictureBook
}
