using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.UI.Navigation;
using Totoro.ViewModels.AnimeDetails;

namespace Totoro.ViewModels;

/// <summary>
/// Details of an anime, shown in the main window's side pane (see AnimeCommands.ShowDetails).
/// Exposes display-ready values rather than the model, so the view binds to flat properties only.
/// </summary>
public partial class AnimeDetailsViewModel : NavigatableViewModel
{
	private readonly IFactory<IAnimeMetadataProvider, Guid> _providers;
	private readonly ILogger<AnimeDetailsViewModel> _logger;

	// The provider the anime came from, and its full details; set once loaded, used by the tabs.
	private IAnimeMetadataProvider? _provider;
	private AnimeModel _details;

	public AnimeDetailsViewModel(AnimeModel anime, IFactory<IAnimeMetadataProvider, Guid> providers, ILogger<AnimeDetailsViewModel> logger)
	{
		_providers = providers;
		_logger = logger;
		Anime = anime;
		_details = anime;

		Episodes = new EpisodesTab(
			() => _provider?.GetEpisodesAsync(_details) ?? Task.FromResult(new List<EpisodeInfo>()),
			() => Anime.Tracking?.WatchedEpisodes ?? 0,
			logger);
		Characters = new CharactersTab(
			() => _provider?.GetCharactersAsync(Anime.Id) ?? Task.FromResult(new List<CharacterModel>()),
			logger);
		Related = new RelatedTab();

		UpdateTracking();
		Anime.PropertyChanged += OnAnimePropertyChanged;
	}

	// Tab keys; the page's SelectorBarItems carry these as their Tag.
	public const string OverviewTab = "Overview";
	public const string EpisodesTabKey = "Episodes";
	public const string CharactersTabKey = "Characters";
	public const string RelatedTabKey = "Related";

	public EpisodesTab Episodes { get; }
	public CharactersTab Characters { get; }
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
	[ObservableProperty] public partial Uri? Link { get; set; }
	[ObservableProperty] public partial string LinkText { get; set; } = "";

	[ObservableProperty] public partial string TrackingStatus { get; set; } = "";
	[ObservableProperty] public partial string TrackingSummary { get; set; } = "";
	[ObservableProperty] public partial string EditTrackingText { get; set; } = "";

	protected override async Task OnNavigatedToInternal()
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
	}

	protected override Task OnNavigatedFromInternal()
	{
		// Anime outlives this view model (it's still in the list); don't let it keep us alive.
		Anime.PropertyChanged -= OnAnimePropertyChanged;
		return Task.CompletedTask;
	}

	private void OnAnimePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AnimeModel.Tracking))
		{
			UpdateTracking();
			if (Episodes.IsLoaded)
			{
				Episodes.UpdateWatched();
			}
		}
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
		Link = Uri.TryCreate(details.Url, UriKind.Absolute, out var uri) ? uri : null;
		LinkText = string.IsNullOrEmpty(details.ServiceName) ? "Open in browser" : $"Open on {details.ServiceName}";
		Related.Show(details);
	}

	private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

	private void UpdateTracking()
	{
		var tracking = Anime.Tracking;

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
			return;
		}

		var watched = tracking.WatchedEpisodes ?? 0;
		var progress = Anime.TotalEpisodes is > 0 and var total ? $"{watched} of {total} episodes" : $"{watched} episodes watched";
		var score = tracking.Score is > 0 ? $"scored {tracking.Score}/10" : "not scored";
		TrackingSummary = $"{progress} · {score}";
		EditTrackingText = "Edit";
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
