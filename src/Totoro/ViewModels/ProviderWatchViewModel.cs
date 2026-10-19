using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;
using Totoro.Playback;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>An episode in the list, e.g. "Episode 5".</summary>
public sealed record EpisodeChoice(ProviderEpisode Episode)
{
	/// <summary>e.g. "5", or "12.5" for a special.</summary>
	public string Number => Episode.Number.ToString(CultureInfo.CurrentCulture);

	public string Label => $"Episode {Number}";

	public override string ToString() => Label;
}

/// <summary>
/// Watches from any provider, whatever anime's settings say: search it and pick a show (cards, with covers from the anime
/// database), then, beside them, an episode and a stream, and play it (see <see cref="WatchService.WatchFromAsync"/>). With tracking on,
/// it's tracked as the anime from the database the user picks (closest title first).
/// </summary>
public sealed partial class ProviderWatchViewModel : NavigatableViewModel
{
	private readonly WatchService _watch;
	private readonly DownloadService _downloads;
	private readonly IFactory<IAnimeMetadataProvider, Guid> _databases;
	private readonly IAnimeSettingsStore _animeSettings;
	private readonly IDialogService _dialogs;
	private readonly List<IModuleSettings> _moduleSettings;
	private readonly Dictionary<Guid, IAnimeProvider> _providers;

	// The show each card is for: a card is the provider's title with the database's cover.
	private readonly Dictionary<AnimeModel, ProviderShow> _cardShows = [];

	public ProviderWatchViewModel(WatchService watch, DownloadService downloads, IFactory<IAnimeProvider, Guid> providers, IFactory<IAnimeMetadataProvider, Guid> databases,
								  IAnimeSettingsStore animeSettings, IDialogService dialogs, IEnumerable<IModuleSettings> moduleSettings,
								  IEnumerable<Descriptor> modules)
	{
		_watch = watch;
		_downloads = downloads;
		_databases = databases;
		_animeSettings = animeSettings;
		_dialogs = dialogs;
		_moduleSettings = [.. moduleSettings];
		Numbering = NumberingChoices[0];
		_providers = providers.CreateAll().ToDictionary(x => x.Id);
		Providers = [.. _providers.Values.Select(x => new ProviderChoice(x.Id, x.Name, modules.FirstOrDefault(m => m.Id == x.Id)?.Image ?? ""))];
		Message = Providers.Count == 0 ? "There's no anime provider: install one from the Plugins page." : "";

		var selected = providers.GetDefaultId();
		SelectedProvider = Providers.FirstOrDefault(x => x.Id == selected) ?? Providers.FirstOrDefault();
	}

	public IReadOnlyList<ProviderChoice> Providers { get; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(EditProviderSettingsCommand))]
	public partial ProviderChoice? SelectedProvider { get; set; }
	[ObservableProperty] public partial string Query { get; set; } = "";

	/// <summary>The shows found, as cards.</summary>
	[ObservableProperty] public partial IReadOnlyList<AnimeModel> ShowCards { get; private set; } = [];

	/// <summary>The card picked; its show's episodes show beside the cards.</summary>
	[ObservableProperty] public partial AnimeModel? SelectedCard { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasShow))]
	public partial ProviderShow? SelectedShow { get; private set; }

	/// <summary>Whether a show is picked: its episodes show then.</summary>
	public bool HasShow => SelectedShow is not null;

	[ObservableProperty] public partial IReadOnlyList<EpisodeChoice> Episodes { get; private set; } = [];
	[ObservableProperty] public partial EpisodeChoice? SelectedEpisode { get; set; }

	[ObservableProperty] public partial IReadOnlyList<VideoServer> Streams { get; private set; } = [];

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(DownloadCommand))]
	public partial VideoServer? SelectedStream { get; set; }

	/// <summary>Whether an episode is picked: its stream, tracking and Play show then.</summary>
	public bool HasEpisode => SelectedEpisode is not null;

	/// <summary>Whether to resume, save progress and mark the episode watched on the list, as <see cref="SelectedAnime"/>.</summary>
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(DownloadCommand))]
	public partial bool Track { get; set; }

	/// <summary>The anime in the database the show may be, best match first.</summary>
	[ObservableProperty] public partial IReadOnlyList<AnimeModel> Matches { get; private set; } = [];

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(DownloadCommand))]
	public partial AnimeModel? SelectedAnime { get; set; }

	public IReadOnlyList<NumberingChoice> NumberingChoices { get; } =
	[
		new("Automatic", null),
		new("By season", EpisodeNumbering.Season),
		new("Absolute", EpisodeNumbering.Absolute),
	];

	/// <summary>How the provider numbers the show's episodes, to work out which of the anime's episode it is (see <see cref="TrackedEpisode"/>).</summary>
	[ObservableProperty] public partial NumberingChoice Numbering { get; set; }

	/// <summary>e.g. "Tracked as episode 1": which of <see cref="SelectedAnime"/>'s episodes the one picked is; empty, never null, until known.</summary>
	[ObservableProperty] public partial string TrackedEpisode { get; private set; } = "";

	[ObservableProperty] public partial bool IsLoading { get; private set; }

	/// <summary>What went wrong or found nothing; empty, never null (it's passed to x:Bind functions).</summary>
	[ObservableProperty] public partial string Message { get; private set; }

	private IAnimeProvider? Provider => SelectedProvider?.Id is { } id ? _providers.GetValueOrDefault(id) : null;

	partial void OnSelectedProviderChanged(ProviderChoice? value) => _ = SearchAsync();

	// The anime's own numbering to start with; the user can change it, here only.
	partial void OnSelectedAnimeChanged(AnimeModel? value)
	{
		if (value is not null)
		{
			Numbering = NumberingChoices.First(x => x.Value == _animeSettings.Get(value).EpisodeNumbering);
		}

		_ = UpdateTrackedEpisodeAsync();
	}

	partial void OnNumberingChanged(NumberingChoice value) => _ = UpdateTrackedEpisodeAsync();

	partial void OnSelectedCardChanged(AnimeModel? value) => SelectedShow = value is null ? null : _cardShows.GetValueOrDefault(value);

	partial void OnSelectedShowChanged(ProviderShow? value)
	{
		Episodes = [];
		SelectedEpisode = null;
		Matches = [];
		SelectedAnime = null;
		if (value is null || Provider is not { } provider)
		{
			return;
		}

		_ = LoadEpisodesAsync(provider, value, null);
		_ = MatchAsync(value);
	}

	/// <summary>Loads the show's episodes, picking the one with id <paramref name="keepEpisode"/> again (with its streams loaded again).</summary>
	private Task LoadEpisodesAsync(IAnimeProvider provider, ProviderShow show, string? keepEpisode)
	{
		return LoadAsync(async () => (await provider.GetEpisodesAsync(show.Id, CancellationToken.None)).OrderBy(x => x.Number).Select(x => new EpisodeChoice(x)).ToList(),
						 () => SelectedShow == show, episodes =>
						 {
							 Episodes = episodes;
							 SelectedEpisode = null;
							 SelectedEpisode = episodes.FirstOrDefault(x => x.Episode.Id == keepEpisode);
							 return episodes.Count > 0 ? "" : $"{provider.Name} has no episodes of it yet.";
						 });
	}

	private bool CanEditProviderSettings() => ProviderSettings is not null;

	private IModuleSettings? ProviderSettings => _moduleSettings.FirstOrDefault(x => x.Descriptor.Id == SelectedProvider?.Id);

	/// <summary>Edits the provider's own settings (as in Settings), then gets the episodes and streams again with them.</summary>
	[RelayCommand(CanExecute = nameof(CanEditProviderSettings))]
	private async Task EditProviderSettingsAsync()
	{
		if (ProviderSettings is { } settings && await _dialogs.EditSettingsAsync(settings) && Provider is { } provider && SelectedShow is { } show)
		{
			await LoadEpisodesAsync(provider, show, SelectedEpisode?.Episode.Id);
		}
	}

	/// <summary>Works out which of the tracked anime's episodes the one picked is, as the numbering says.</summary>
	private async Task UpdateTrackedEpisodeAsync()
	{
		TrackedEpisode = "";
		if (SelectedAnime is not { } anime || SelectedEpisode is not { } episode)
		{
			return;
		}

		var number = await AnimeEpisodeAsync(anime, episode);
		if (SelectedAnime == anime && SelectedEpisode == episode)
		{
			TrackedEpisode = $"Tracked as episode {number}";
		}
	}

	private Task<int> AnimeEpisodeAsync(AnimeModel anime, EpisodeChoice episode) =>
		_watch.GetAnimeEpisodeAsync(anime, [.. Episodes.Select(x => x.Episode)], episode.Episode, Numbering.Value, CancellationToken.None);

	partial void OnSelectedEpisodeChanged(EpisodeChoice? value)
	{
		OnPropertyChanged(nameof(HasEpisode));
		_ = UpdateTrackedEpisodeAsync();
		Streams = [];
		SelectedStream = null;
		if (value is not null && Provider is { } provider)
		{
			_ = LoadAsync(() => provider.GetServersAsync(value.Episode, CancellationToken.None), () => SelectedEpisode == value, streams =>
			{
				Streams = streams;
				SelectedStream = streams.FirstOrDefault();
				return streams.Count > 0 ? "" : $"{provider.Name} has no streams of it.";
			});
		}
	}

	[RelayCommand]
	private async Task SearchAsync()
	{
		ShowCards = [];
		_cardShows.Clear();
		SelectedCard = null;
		if (Provider is not { } provider || string.IsNullOrWhiteSpace(Query))
		{
			return;
		}

		var query = Query;
		await LoadAsync(() => provider.SearchAsync(query, CancellationToken.None), () => Provider == provider && Query == query, shows =>
		{
			List<AnimeModel> cards = [];
			foreach (var show in shows)
			{
				// The provider's own cover, if it has one
				var card = new AnimeModel { Title = show.Title, Image = show.Image };
				_cardShows[card] = show;
				cards.Add(card);
			}

			ShowCards = cards;
			return shows.Count > 0 ? "" : $"No shows on {provider.Name} match. Try another title.";
		});
	}

	/// <summary>The database's anime for a search; none if it fails (they're only suggestions).</summary>
	private async Task<List<AnimeModel>> SearchDatabaseAsync(string query)
	{
		try
		{
			return _databases.CreateDefault() is { } database ? await database.SearchAnimeAsync(query) : [];
		}
		catch (Exception)
		{
			return [];
		}
	}

	/// <summary>How close the anime's closest title (of its titles in each language, and others) is to the show's, from 0 to 1.</summary>
	private static double Closeness(string showTitle, AnimeModel anime)
	{
		return new[] { anime.Title, anime.EngTitle, anime.RomajiTitle }.Concat(anime.AlternateTitles)
																	   .Where(t => !string.IsNullOrWhiteSpace(t))
																	   .Select(t => TitleMatch.Score(showTitle, t))
																	   .DefaultIfEmpty(0)
																	   .Max();
	}

	/// <summary>Finds the anime in the database the show may be, to track it as; the user can pick another.</summary>
	private async Task MatchAsync(ProviderShow show)
	{
		// The database's own order puts a sequel first as often as not: closest title first, theirs for ties.
		List<AnimeModel> matches = [.. (await SearchDatabaseAsync(show.Title)).OrderByDescending(x => Closeness(show.Title, x))];
		if (SelectedShow == show)
		{
			Matches = matches;
			SelectedAnime = matches.FirstOrDefault();
		}
	}

	/// <summary>Loads something from the provider and shows it, unless the selection it was for has changed meanwhile.</summary>
	private async Task LoadAsync<T>(Func<Task<T>> load, Func<bool> isCurrent, Func<T, string> show)
	{
		IsLoading = true;
		Message = "";
		try
		{
			var result = await load();
			if (isCurrent())
			{
				Message = show(result);
			}
		}
		catch (Exception ex) when (isCurrent())
		{
			Message = $"Couldn't get it from {SelectedProvider?.Label}: {ex.Message}";
		}
		catch (Exception)
		{
			// For a selection that's since changed: nobody's waiting for it.
		}
		finally
		{
			IsLoading = false;
		}
	}

	private bool CanPlay() => SelectedStream is not null && (!Track || SelectedAnime is not null);

	[RelayCommand(CanExecute = nameof(CanPlay), AllowConcurrentExecutions = true)]
	// Concurrent: playback lasts until the player closes; starting another replaces it.
	private async Task PlayAsync()
	{
		if (Provider is not { } provider || SelectedShow is not { } show || SelectedEpisode is not { } episode || SelectedStream is not { } stream)
		{
			return;
		}

		if (Track && SelectedAnime is { } tracked)
		{
			await _watch.WatchFromAsync(tracked, await AnimeEpisodeAsync(tracked, episode), provider, episode.Episode, stream, track: true);
			return;
		}

		// Untracked, a match may be the wrong season: its title and skip times would be too, so the show's own title alone.
		await _watch.WatchFromAsync(new AnimeModel { Title = show.Title }, (int)episode.Episode.Number, provider, episode.Episode, stream, track: false);
	}

	/// <summary>
	/// Downloads the stream in the background (the Downloads page shows how it goes): as the tracked anime's episode, in a
	/// folder named with its ids, or untracked as the show's.
	/// </summary>
	[RelayCommand(CanExecute = nameof(CanPlay), AllowConcurrentExecutions = true)]
	private async Task DownloadAsync()
	{
		if (SelectedShow is not { } show || SelectedEpisode is not { } episode || SelectedStream is not { } stream)
		{
			return;
		}

		if (Track && SelectedAnime is { } tracked)
		{
			await _downloads.DownloadAsync(tracked, tracked.Title, await AnimeEpisodeAsync(tracked, episode), stream);
			return;
		}

		await _downloads.DownloadAsync(null, show.Title, (int)episode.Episode.Number, stream);
	}
}
