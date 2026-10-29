using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Services;
using Totoro.Playback;
using Totoro.UI.Navigation;
using Totoro.ViewModels.AnimeDetails;

namespace Totoro.ViewModels;

/// <summary>
/// Details of an anime, shown in the main window's side pane (see AnimeCommands.ShowDetails).
/// Exposes display-ready values rather than the model, so the view binds to flat properties only.
/// </summary>
public partial class AnimeDetailsViewModel : NavigatableViewModel, IRecipient<PlaybackProgressChangedMessage>,
	IRecipient<AnimeTrackingChangedMessage>
{
	private readonly IFactory<IAnimeMetadataProvider, Guid> _providers;
	private readonly ILogger<AnimeDetailsViewModel> _logger;
	private readonly IPlaybackProgressStore _progress;
	// The anime databases installed, and their icons (see Links).
	private readonly List<Descriptor> _modules;

	// The provider the anime came from, and its full details; set once loaded, used by the tabs.
	private IAnimeMetadataProvider? _provider;
	private AnimeModel _details;

	public AnimeDetailsViewModel(
		AnimeModel anime,
		IFactory<IAnimeMetadataProvider, Guid> providers,
		IPlaybackProgressStore progress,
		AnimeSongService songs,
		ISongPlayer songPlayer,
		IEnumerable<Descriptor> modules,
		ILogger<AnimeDetailsViewModel> logger)
	{
		_providers = providers;
		_modules = [.. modules];
		_logger = logger;
		_progress = progress;
		Anime = anime;
		_details = anime;

		Episodes = new EpisodesTab(
			() => _provider?.GetEpisodesAsync(_details) ?? Task.FromResult(new List<EpisodeInfo>()),
			anime,
			progress,
			logger);
		Characters = new CharactersTab(
			() => _provider?.GetCharactersAsync(Anime.Id) ?? Task.FromResult(new List<CharacterModel>()),
			logger);
		Music = new MusicTab(() => songs.GetSongsAsync(_details), song => new SongItem(_details, song, songs, songPlayer), songPlayer, logger);
		Related = new RelatedTab();

		UpdateTracking();
		Anime.PropertyChanged += OnAnimePropertyChanged;
		WeakReferenceMessenger.Default.RegisterAll(this);
	}

	// Tab keys; the page's SelectorBarItems carry these as their Tag.
	public const string OverviewTab = "Overview";
	public const string EpisodesTabKey = "Episodes";
	public const string CharactersTabKey = "Characters";
	public const string MusicTabKey = "Music";
	public const string RelatedTabKey = "Related";

	public EpisodesTab Episodes { get; }
	public CharactersTab Characters { get; }
	public MusicTab Music { get; }
	public RelatedTab Related { get; }

	/// <summary>The key of the tab being shown; a tab loads its content the first time it's selected.</summary>
	[ObservableProperty]
	public partial string SelectedTab { get; set; } = OverviewTab;

	partial void OnSelectedTabChanged(string value)
	{
		DetailsTab? tab = value switch
		{
			EpisodesTabKey => Episodes,
			CharactersTabKey => Characters,
			MusicTabKey => Music,
			RelatedTabKey => Related,
			_ => null,
		};

		// Only lazy tabs have anything to load; errors are handled (and shown) by the tab itself.
		_ = tab?.EnsureLoadedAsync();
	}

	/// <summary>
	/// Set once the details are filled in (with the full details, or the anime as given if they couldn't load).
	/// The details are only filled in once, so the view can show them all at once instead of piece by piece.
	/// </summary>
	[ObservableProperty]
	public partial bool IsLoaded { get; set; }

	/// <summary>
	/// The anime as the caller has it (e.g. a card in the user list). Tracking is read from and edited on this
	/// instance, so changes show up wherever else it's displayed.
	/// </summary>
	public AnimeModel Anime { get; }

	[ObservableProperty] public partial string Title { get; set; } = "";
	[ObservableProperty] public partial string Subtitle { get; set; } = "";
	// Image URLs, or null when missing; the view turns them into image sources (see CommonConverters.ToImageSource).
	[ObservableProperty] public partial string? BannerImage { get; set; }
	[ObservableProperty] public partial string? CoverImage { get; set; }
	[ObservableProperty] public partial IReadOnlyList<string> Facts { get; set; } = [];
	[ObservableProperty] public partial IReadOnlyList<string> Genres { get; set; } = [];
	[ObservableProperty] public partial string Description { get; set; } = "";
	[ObservableProperty] public partial string Studios { get; set; } = "";
	/// <summary>Its videos: trailers and PVs (AniList has one, MyAnimeList often several), each opening in the browser.</summary>
	[ObservableProperty] public partial IReadOnlyList<TrailerItem> Trailers { get; set; } = [];
	/// <summary>Its pages on the anime databases, then the official sites streaming it (AniList only); each opens in the browser.</summary>
	[ObservableProperty] public partial IReadOnlyList<LinkItem> Links { get; set; } = [];

	[ObservableProperty] public partial string TrackingStatus { get; set; } = "";
	[ObservableProperty] public partial string TrackingSummary { get; set; } = "";

	/// <summary>The tracking button's tooltip and accessible name: "Add to list" or "Edit tracking".</summary>
	[ObservableProperty] public partial string EditTrackingText { get; set; } = "";

	/// <summary>The tracking button's icon: Add when it isn't on the list, Edit when it is.</summary>
	[ObservableProperty] public partial string EditTrackingGlyph { get; set; } = "";

	/// <summary>
	/// The play button's text, e.g. "Play episode 5" or "Resume episode 5"; empty (never null, it's passed to x:Bind
	/// functions) once every episode is watched.
	/// </summary>
	[ObservableProperty] public partial string PlayText { get; set; } = "";

	protected override async Task OnNavigatedToInternalAsync()
	{
		// The full details have studios, related anime etc. that the list's model may lack; fall back to the
		// anime as given if they can't be loaded.
		var details = Anime;

		// Ask the provider the anime came from; ids aren't shared between databases.
		_provider = Anime.ServiceId is { } serviceId ? _providers.Create(serviceId) : _providers.CreateDefault();
		if (_provider is not null)
		{
			try
			{
				details = await _provider.GetAnimeAsync(Anime.Id);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to load details for {Anime}", Anime.Title);
			}
		}

		_details = details;
		ShowDetails(details);
		IsLoaded = true;
		await FindDatabaseIdsAsync(details);
	}

	/// <summary>
	/// The anime databases it has a page on, by <see cref="AnimeId"/> property (and module) name, and the page's address
	/// for its id there. Shikimori numbers its anime with MyAnimeList's ids.
	/// </summary>
	private static readonly (string Name, string Url)[] s_databases =
	[
		(nameof(AnimeId.Anilist), "https://anilist.co/anime/{0}/"),
		(nameof(AnimeId.MyAnimeList), "https://myanimelist.net/anime/{0}"),
		(nameof(AnimeId.Kitsu), "https://kitsu.app/anime/{0}"),
		(nameof(AnimeId.Shikimori), "https://shikimori.io/animes/{0}"),
	];

	private Descriptor? FindModule(string name) => _modules.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

	/// <summary>Its ids in the installed databases it didn't come with (e.g. Kitsu's), looked up together on ani.zip, for their links.</summary>
	private async Task FindDatabaseIdsAsync(AnimeModel details)
	{
		if (s_databases.FirstOrDefault(x => FindModule(x.Name) is not null && details.ExternalIds.GetIdForService(x.Name) is not > 0) is not { Name: { } missing })
		{
			return;
		}

		try
		{
			await details.FindIdAsync(missing);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to find the database ids of {Anime}", details.Title);
			return;
		}

		ShowLinks(details);
	}

	/// <summary>Its page on the database it came from (as that gave it), then on each other one installed, then where it streams.</summary>
	private void ShowLinks(AnimeModel details)
	{
		var own = LinkItem.Create(string.IsNullOrEmpty(details.ServiceName) ? "Open in browser" : $"Open on {details.ServiceName}",
								  details.Url, _modules.FirstOrDefault(x => x.Id == details.ServiceId)?.Image, null);
		var others = s_databases.Where(x => !x.Name.Equals(details.ServiceName, StringComparison.OrdinalIgnoreCase))
								.Select(x => (Module: FindModule(x.Name), Id: details.ExternalIds.GetIdForService(x.Name), x.Url))
								.Where(x => x is { Module: not null, Id: > 0 })
								.Select(x => LinkItem.Create($"Open on {x.Module!.Name}", string.Format(CultureInfo.InvariantCulture, x.Url, x.Id), x.Module.Image, null));
		Links = [.. others.Prepend(own).Concat(details.StreamingLinks.Select(LinkItem.From)).OfType<LinkItem>()];
	}

	protected override Task OnNavigatedFromInternalAsync()
	{
		// Anime outlives this view model (it's still in the list); don't let it keep us alive.
		Anime.PropertyChanged -= OnAnimePropertyChanged;
		WeakReferenceMessenger.Default.UnregisterAll(this);
		return Task.CompletedTask;
	}

	/// <summary>Playing an episode saved (or cleared) its progress: show it in the button and the episode list.</summary>
	public void Receive(PlaybackProgressChangedMessage message)
	{
		if (message.AnimeKey == Anime.StoreKey)
		{
			UpdatePlayText();
			if (Episodes.IsLoaded)
			{
				Episodes.Refresh();
			}
		}
	}

	/// <summary>The anime's list entry was changed through another instance of it (e.g. by an agent tool): show it.</summary>
	public void Receive(AnimeTrackingChangedMessage message)
	{
		if (!ReferenceEquals(message.Anime, Anime) && message.Anime.StoreKey == Anime.StoreKey)
		{
			Anime.Tracking = message.Tracking;
		}
	}

	private void OnAnimePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AnimeModel.Tracking))
		{
			UpdateTracking();
			if (Episodes.IsLoaded)
			{
				Episodes.Refresh();
			}
		}
	}

	private void UpdatePlayText()
	{
		var next = WatchService.NextEpisode(Anime);
		PlayText = Anime.TotalEpisodes is > 0 and var total && next > total ? ""
			: _progress.Get(Anime, next) is not null ? $"Resume episode {next}"
			: $"Play episode {next}";
	}

	private void ShowDetails(AnimeModel details)
	{
		Title = details.Title;
		// The English title, when it differs from the main (romaji) one.
		Subtitle = string.Equals(details.EngTitle, details.Title, StringComparison.OrdinalIgnoreCase) ? "" : details.EngTitle;
		CoverImage = NullIfEmpty(details.Image);
		BannerImage = NullIfEmpty(details.BannerImage) ?? CoverImage;
		Facts = GetFacts(details);
		Genres = [.. details.Genres];
		Description = details.Description;
		Studios = string.Join(", ", details.Studios);
		Trailers = [.. details.Trailers.Select(TrailerItem.From).OfType<TrailerItem>()];
		ShowLinks(details);
		Related.Show(details);
	}

	private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

	private void UpdateTracking()
	{
		var tracking = Anime.Tracking;
		UpdatePlayText();

		TrackingStatus = tracking?.Status switch
		{
			null => "Not on your list",
			ListItemStatus.Watching => "Watching",
			ListItemStatus.PlanToWatch => "Plan to watch",
			ListItemStatus.Completed => "Completed",
			ListItemStatus.OnHold => "On hold",
			ListItemStatus.Dropped => "Dropped",
			ListItemStatus.Rewatching => "Rewatching",
			_ => tracking.Status.ToString()!,
		};

		if (tracking?.Status is null)
		{
			TrackingSummary = "Add it to keep track of your progress.";
			EditTrackingText = "Add to list";
			EditTrackingGlyph = "";
			return;
		}

		var watched = tracking.WatchedEpisodes ?? 0;
		var progress = Anime.TotalEpisodes is > 0 and var total ? $"{watched} of {total} episodes" : $"{watched} episodes watched";
		var score = tracking.Score is > 0 ? $"scored {tracking.Score}/10" : "not scored";
		TrackingSummary = $"{progress} · {score}";
		EditTrackingText = "Edit tracking";
		EditTrackingGlyph = "";
	}

	private static List<string> GetFacts(AnimeModel anime)
	{
		List<string> facts = [];

		var format = anime.MediaFormat switch
		{
			AnimeMediaFormat.Tv => "TV",
			AnimeMediaFormat.Ova => "OVA",
			AnimeMediaFormat.Ona => "ONA",
			AnimeMediaFormat.Movie => "Movie",
			AnimeMediaFormat.Special => "Special",
			AnimeMediaFormat.Music => "Music",
			_ => null,
		};
		if (format is not null)
		{
			facts.Add(format);
		}

		facts.Add(anime.AiringStatus switch
		{
			AiringStatus.CurrentlyAiring => "Airing",
			AiringStatus.NotYetAired => "Not yet aired",
			_ => "Finished",
		});

		if (anime.TotalEpisodes is > 0 and var total)
		{
			facts.Add(total == 1 ? "1 episode" : $"{total} episodes");
		}

		if (anime.MeanScore is > 0 and var score)
		{
			// AniList's mean score is out of 100.
			facts.Add($"★ {score / 10:0.0}");
		}

		if (anime.Season is { } season)
		{
			facts.Add($"{season.SeasonName} {season.Year}");
		}

		return facts;
	}
}

/// <summary>
/// A link the overview shows as an icon button, e.g. "Watch on Netflix (Japanese)": opening <see cref="Url"/> in the browser.
/// </summary>
/// <param name="Name">Its tooltip and accessible name.</param>
/// <param name="Icon">Its icon's file or URL; empty (never null, for x:Bind) when there's none.</param>
/// <param name="Color">The icon's background, e.g. "#F88C00"; null for the card's own.</param>
public sealed record LinkItem(string Name, Uri Url, string Icon, string? Color)
{
	/// <summary>A glyph on a color sits inside its tile; a logo of its own (the database's) fills it.</summary>
	public double IconSize => Color is null ? 40 : 24;

	public static LinkItem? From(StreamingLink link) =>
		Create($"Watch on {(link.Language is null ? link.Site : $"{link.Site} ({link.Language})")}", link.Url, link.Icon, link.Color);

	public static LinkItem? Create(string name, string url, string? icon, string? color)
	{
		return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
			? new LinkItem(name, uri, icon ?? "", color)
			: null;
	}
}

/// <summary>A trailer, as the overview shows it: opening its <see cref="Url"/> (YouTube) in the browser.</summary>
/// <param name="Thumbnail">Its thumbnail's URL; empty (never null, for x:Bind) when there's none.</param>
public sealed record TrailerItem(string Title, string Thumbnail, Uri Url)
{
	/// <summary>Its accessible name, e.g. "Watch Trailer in the browser".</summary>
	public string Name => $"Watch {Title} in the browser";

	public static TrailerItem? From(TrailerVideo video)
	{
		return Uri.TryCreate(video.Url, UriKind.Absolute, out var url)
			? new TrailerItem(string.IsNullOrWhiteSpace(video.Title) ? "Trailer" : video.Title, Widescreen(video.Thumbnail ?? ""), url)
			: null;
	}

	/// <summary>
	/// YouTube's 16:9 thumbnail (mqdefault) for its 4:3 one (hqdefault, which AniList gives), whose black bars would show
	/// in the 16:9 frame.
	/// </summary>
	private static string Widescreen(string thumbnail)
	{
		return thumbnail.Contains("ytimg.com", StringComparison.OrdinalIgnoreCase)
			? thumbnail.Replace("/hqdefault.", "/mqdefault.", StringComparison.OrdinalIgnoreCase)
			: thumbnail;
	}
}
