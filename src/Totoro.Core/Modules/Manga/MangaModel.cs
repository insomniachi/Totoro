using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Modules.Manga;

[DebuggerDisplay("{Title}")]
public partial class MangaModel : ObservableObject, IMediaModel
{
	public long Id { get; init; }
	public MangaId ExternalIds { get; init; } = new();
	public string Image { get; set; } = "";
	public string BannerImage { get; set; } = "";
	public string Title { get; init; } = "";
	public string EngTitle { get; set; } = "";
	public string RomajiTitle { get; set; } = "";
	[ObservableProperty] public partial MangaTracking? Tracking { get; set; }
	public int? TotalChapters { get; set; }
	public int? TotalVolumes { get; set; }
	/// <summary>Whether it's still being published; reuses the anime one (Currently Airing is "Publishing").</summary>
	public AiringStatus Status { get; set; }
	/// <summary>When it started coming out, to the month (its day is the 1st when the source didn't say); null if not known.</summary>
	public DateTime? StartDate { get; set; }
	/// <summary>The average score, out of 100.</summary>
	public float? MeanScore { get; set; }
	public int Popularity { get; set; }
	public Guid? ServiceId { get; init; }
	public string? ServiceName { get; init; }
	/// <summary>HTML, like <see cref="AnimeModel.Description"/>.</summary>
	public string Description { get; set; } = "";
	public string Url { get; init; } = "";
	public MangaFormat Format { get; init; } = MangaFormat.Unknown;
	public IReadOnlyCollection<string> Genres { get; init; } = [];
	public IReadOnlyCollection<string> AlternateTitles { get; init; } = [];

	/// <summary>Its titles, the main one first, without blanks or repeats: what sources are searched with.</summary>
	public IEnumerable<string> AllTitles => new[] { Title, EngTitle, RomajiTitle }.Concat(AlternateTitles)
		.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase);

	public override string ToString() => Title;

	Navigation.MediaMode IMediaModel.Kind => Navigation.MediaMode.Manga;
	ListItemStatus? IMediaModel.ListStatus => Tracking?.Status;
	double? IMediaModel.UserScore => Tracking?.Score;
	int? IMediaModel.Progress => Tracking?.ChaptersRead;
	int? IMediaModel.Total => TotalChapters;
	DateTime? IMediaModel.StartDate => Tracking?.StartDate;
	DateTime? IMediaModel.FinishDate => Tracking?.FinishDate;
	DateTime? IMediaModel.UpdatedAt => Tracking?.UpdatedAt;
}

public sealed class MangaId
{
	public long MyAnimeList { get; set; }
	public long Anilist { get; set; }
	public long Kitsu { get; set; }
	/// <summary>Shikimori numbers its manga with MyAnimeList's ids.</summary>
	public long Shikimori => MyAnimeList;
}

public enum MangaFormat
{
	Unknown,
	Manga,
	[Description("Manhwa")] Manhwa,
	[Description("Manhua")] Manhua,
	[Description("One-shot")] OneShot,
	[Description("Light novel")] Novel,
}
