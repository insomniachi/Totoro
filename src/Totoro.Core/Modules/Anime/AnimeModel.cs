using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Settings;

namespace Totoro.Core.Modules.Anime;

[DebuggerDisplay("{Title}")]
public partial class AnimeModel : ObservableObject, IMediaModel
{
	public long Id { get; init; }
	public AnimeId ExternalIds { get; init; } = new();
	public string Image { get; set; } = "";
	public string BannerImage { get; set; } = "";
	private readonly string _title = "";

	/// <summary>The anime database's title, or the one in the language the user picked for this anime (see <see cref="AnimeSettings.TitleLanguage"/>).</summary>
	public string Title
	{
		get => TitleLanguageOf?.Invoke(this) switch
		{
			TitleLanguage.English when EngTitle.Length > 0 => EngTitle,
			TitleLanguage.Romaji when RomajiTitle.Length > 0 => RomajiTitle,
			_ => _title,
		};
		init => _title = value;
	}

	public string EngTitle { get; set; } = "";
	public string RomajiTitle { get; set; } = "";

	/// <summary>Its English or romaji title, whichever isn't <see cref="Title"/>, to show beside it; "" if it has just the one.</summary>
	[JsonIgnore]
	public string OtherTitle => new[] { EngTitle, RomajiTitle }.FirstOrDefault(x => x.Length > 0 && !x.Equals(Title, StringComparison.OrdinalIgnoreCase)) ?? "";

	/// <summary>The title language the user picked for an anime, if any; set by the anime settings store.</summary>
	public static Func<AnimeModel, TitleLanguage?>? TitleLanguageOf { get; set; }

	/// <summary>Tells bindings its <see cref="Title"/> changed, after its title language did.</summary>
	public void RefreshTitle() => OnPropertyChanged(nameof(Title));
	[ObservableProperty] public partial Tracking? Tracking { get; set; }
	public int? TotalEpisodes { get; set; }
	[ObservableProperty] public partial AiringStatus AiringStatus { get; set; }
	/// <summary>The average score, out of 100.</summary>
	/// <summary>The average score, out of 100 (from every database, whatever its own scale).</summary>
	public float? MeanScore { get; set; }
	public int Popularity { get; set; }
	public DateTime? NextEpisodeAt { get; set; }
	public int AiredEpisodes { get; set; }
	/// <summary>The latest episode the user's anime provider has, once it's been checked; null until then (or if it couldn't be).</summary>
	[ObservableProperty] public partial int? ProviderEpisodes { get; set; }
	/// <summary>When each episode airs (or aired), in local time, by number, if the database knows (AniList does); else empty.</summary>
	public IReadOnlyDictionary<int, DateTime> AiringSchedule { get; set; } = new Dictionary<int, DateTime>();
	/// <summary>When it finished (or will finish) airing, if known: the day, in its own time zone.</summary>
	public DateTime? EndDate { get; set; }
	public Season? Season { get; set; }
	public Guid? ServiceId { get; init; }
	public string? ServiceName { get; init; }
	/// <summary>HTML (AniList renders all of its markup to HTML), which views render, e.g. with Totoro.UI.Text.HtmlText.</summary>
	public string Description { get; set; } = "";
	public IEnumerable<AnimeModel> Related { get; set; } = [];

	/// <summary>How it relates to the anime whose <see cref="Related"/> it's in (e.g. its sequel); null outside such a list.</summary>
	public AnimeRelationType? Relation { get; set; }

	public IEnumerable<AnimeModel> Recommended { get; set; } = [];
	public List<EpisodeInfo> Episodes { get; init; } = [];
	public string Url { get; init; } = "";
	public AnimeMediaFormat MediaFormat { get; init; } = AnimeMediaFormat.Unknown;
	public IReadOnlyCollection<string> Genres { get; init; } = [];
	public IReadOnlyCollection<string> Studios { get; init; } = [];
	public IReadOnlyCollection<TrailerVideo> Trailers { get; set; } = [];
	/// <summary>Where it streams legally (e.g. Crunchyroll, Netflix); only AniList gives these.</summary>
	public IReadOnlyCollection<StreamingLink> StreamingLinks { get; init; } = [];
	public IReadOnlyCollection<string> AlternateTitles { get; init; } = [];

	// What screen readers announce for it as an item of a list control, e.g. the Discover page's spotlight.
	public override string ToString() => Title;

	Navigation.MediaMode IMediaModel.Kind => Navigation.MediaMode.Anime;
	ListItemStatus? IMediaModel.ListStatus => Tracking?.Status;
	double? IMediaModel.UserScore => Tracking?.Score;
	int? IMediaModel.Progress => Tracking?.WatchedEpisodes;
	int? IMediaModel.Total => TotalEpisodes;
	DateTime? IMediaModel.StartDate => Tracking?.StartDate;
	DateTime? IMediaModel.FinishDate => Tracking?.FinishDate;
	DateTime? IMediaModel.UpdatedAt => Tracking?.UpdatedAt;
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
	/// <summary>Shikimori numbers its anime with MyAnimeList's ids.</summary>
	public long Shikimori => MyAnimeList;

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

/// <summary>How an anime relates to another, e.g. as its sequel.</summary>
public enum AnimeRelationType
{
	Sequel,
	Prequel,
	SideStory,
	/// <summary>The story it's part of, e.g. a side story's main series.</summary>
	Parent,
	Alternative,
	Summary,
	SpinOff,
	Other,
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

/// <summary>
/// An official site streaming an anime, and its page there. <paramref name="Icon"/> is the site's icon's URL (a white
/// glyph, for a background of <paramref name="Color"/>, e.g. "#F88C00"); those and <paramref name="Language"/> are null
/// when not given.
/// </summary>
public sealed record StreamingLink(string Site, string Url, string? Language, string? Icon = null, string? Color = null);


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

	/// <summary>Whether it's filler (not from the source material), as MyAnimeList says.</summary>
	[JsonIgnore] public bool IsFiller { get; set; }

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