using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Totoro.Core.Modules.Anime;

[DebuggerDisplay("{Title}")]
public partial class AnimeModel : ObservableObject
{
	public long Id { get; init; }
	public AnimeId ExternalIds { get; init; } = new();
	public string Image { get; set; } = "";
	public string BannerImage { get; set; } = "";
	public string Title { get; init; } = "";
	public string EngTitle { get; set; } = "";
	public string RomajiTitle { get; set; } = "";
	[ObservableProperty] public partial Tracking? Tracking { get; set; }
	public int? TotalEpisodes { get; set; }
	[ObservableProperty] public partial AiringStatus AiringStatus { get; set; }
	/// <summary>The average score, out of 100.</summary>
	/// <summary>The average score, out of 100 (from every database, whatever its own scale).</summary>
	public float? MeanScore { get; set; }
	public int Popularity { get; set; }
	public DateTime? NextEpisodeAt { get; set; }
	public int AiredEpisodes { get; set; }
	public Season? Season { get; set; }
	public Guid? ServiceId { get; init; }
	public string? ServiceName { get; init; }
	/// <summary>HTML (AniList renders all of its markup to HTML), which views render, e.g. with Totoro.UI.Text.HtmlText.</summary>
	public string Description { get; set; } = "";
	public IEnumerable<AnimeModel> Related { get; set; } = [];
	public IEnumerable<AnimeModel> Recommended { get; set; } = [];
	public List<EpisodeInfo> Episodes { get; init; } = [];
	public string Url { get; init; } = "";
	public AnimeMediaFormat MediaFormat { get; init; } = AnimeMediaFormat.Unknown;
	public IReadOnlyCollection<string> Genres { get; init; } = [];
	public IReadOnlyCollection<string> Studios { get; init; } = [];
	public IReadOnlyCollection<TrailerVideo> Trailers { get; set; } = [];
	public IReadOnlyCollection<string> AlternateTitles { get; init; } = [];
}


public sealed class AnimeId
{
	public long AniDb { get; set; }
	public long MyAnimeList { get; set; }
	public long Anilist { get; set; }
	public long Kitsu { get; set; }
	public long Simkl { get; set; }
	public long AnimeNewsNetwork { get; set; }
	public long Local => Anilist;

	public long? GetIdForService(string serviceType)
	{
		if (GetType().GetProperties().FirstOrDefault(x => x.Name.Equals(serviceType, StringComparison.OrdinalIgnoreCase)) is not { } property)
		{
			return null;
		}

		return (long?)property.GetValue(this);
	}

	public static bool EqualsForAnimeRelations(AnimeId first, AnimeId second)
	{
		return first.MyAnimeList == second.MyAnimeList &&
			   first.Anilist == second.Anilist &&
			   first.Kitsu == second.Kitsu;
	}
}

public enum AiringStatus
{
	[Description("Finished Airing")] FinishedAiring,

	[Description("Currently Airing")] CurrentlyAiring,

	[Description("Not Yet Aired")] NotYetAired
}

public sealed record Season(AnimeSeason SeasonName, int Year)
{
	public Season() : this(AnimeSeason.Winter, 1900)
	{

	}

	/// <summary>The anime season <paramref name="date"/> falls in, as AniList counts them (Winter is January to March).</summary>
	public static Season Of(DateTime date) => new((AnimeSeason)((date.Month - 1) / 3), date.Year);
}

public enum AnimeSeason
{
	Winter,
	Spring,
	Summer,
	Fall
}

public enum AnimeMediaFormat
{
	Unknown,
	Tv,
	Ova,
	Movie,
	Special,
	Ona,
	Music
}

public class TrailerVideo
{
	public string Url { get; set; } = "";
	public string Title { get; set; } = "";
	public string Thumbnail { get; set; } = "";
}


public class EpisodeInfo
{
	[JsonPropertyName("seasonNumber")] public int SeasonNumber { get; set; }

	[JsonPropertyName("episodeNumber")] public int EpisodeNumber { get; set; }

	[JsonPropertyName("absoluteEpisodeNumber")]
	public int AbsoluteEpisodeNumber { get; set; }

	[JsonPropertyName("title")] public Titles Titles { get; set; } = new();

	[JsonPropertyName("overview")] public string Overview { get; set; } = "";

	[JsonPropertyName("image")] public string Image { get; set; } = "";

	[JsonPropertyName("airDate")] public string AirDate { get; set; } = "";

	[JsonPropertyName("runtime")] public int Runtime { get; set; }

	[JsonPropertyName("airDateUtc")] public DateTimeOffset? AirDateUtc { get; set; }

	[JsonIgnore] public ProgressInfo? Progress { get; set; }

	[JsonIgnore] public bool IsSpecial { get; set; }

	public void UpdateIfNotPresent(EpisodeInfo newInfo)
	{
		if (string.IsNullOrEmpty(Titles.Japanese))
			Titles.Japanese = newInfo.Titles.Japanese;
		if (string.IsNullOrEmpty(Titles.English))
			Titles.English = newInfo.Titles.English;
		if (string.IsNullOrEmpty(Titles.Romaji))
			Titles.Romaji = newInfo.Titles.Romaji;
		if (string.IsNullOrEmpty(Overview))
			Overview = newInfo.Overview;
		if (string.IsNullOrEmpty(Image))
			Image = newInfo.Image;
		if (string.IsNullOrEmpty(AirDate))
			AirDate = newInfo.AirDate;
		if (Runtime == 0)
			Runtime = newInfo.Runtime;
		AirDateUtc ??= newInfo.AirDateUtc;
	}
}

public class Titles
{
	[JsonPropertyName("ja")] public string Japanese { get; set; } = "";

	[JsonPropertyName("en")] public string English { get; set; } = "";

	[JsonPropertyName("x-jat")] public string Romaji { get; set; } = "";
}

public class ProgressInfo
{
	public double Position { get; set; }

	public double Total { get; set; }

	[JsonIgnore] public bool IsCompleted { get; set; }
}