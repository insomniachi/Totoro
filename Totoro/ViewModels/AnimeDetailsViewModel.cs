using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>
/// Details of an anime, shown in the main window's side pane (see AnimeCommands.ShowDetails).
/// Exposes display-ready values rather than the model, so the view binds to flat properties only.
/// </summary>
public partial class AnimeDetailsViewModel : NavigatableViewModel
{
	private readonly IFactory<IAnimeMetadataProvider, Guid> _providers;
	private readonly ILogger<AnimeDetailsViewModel> _logger;

	public AnimeDetailsViewModel(AnimeModel anime, IFactory<IAnimeMetadataProvider, Guid> providers, ILogger<AnimeDetailsViewModel> logger)
	{
		_providers = providers;
		_logger = logger;
		Anime = anime;

		UpdateTracking();
		Anime.PropertyChanged += OnAnimePropertyChanged;
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
	// Image URLs, or null when missing; the view turns them into image sources (see AnimeDetailsPage.ToImageSource).
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
		var provider = Anime.ServiceId is { } serviceId ? _providers.Create(serviceId) : _providers.CreateDefault();
		if (provider is not null)
		{
			try
			{
				details = await provider.GetAnimeAsync(Anime.Id);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to load details for {Anime}", Anime.Title);
			}
		}

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
