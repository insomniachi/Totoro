using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Commands;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;
using Totoro.Core.Services;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels.Manga;

/// <summary>
/// A manga's details, in the side pane: what it is, the user's entry, and its chapters on the manga source to read
/// from. When the source's match is wrong (or there's none), the user searches the source and picks the right one.
/// </summary>
public partial class MangaDetailsViewModel : NavigatableViewModel
{
	private readonly MangaReadingService _reading;
	private CancellationTokenSource? _load;

	public MangaDetailsViewModel(MangaModel manga, MangaReadingService reading)
	{
		Manga = manga;
		_reading = reading;
		Sources = [.. reading.Sources];
		// The settings' one, as one of Sources: the factory makes a new instance each time, which the box wouldn't find.
		Source = Sources.FirstOrDefault(x => x.Id == reading.Source?.Id);
		manga.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(MangaModel.Tracking))
			{
				OnPropertyChanged(nameof(ReadText));
				OnPropertyChanged(nameof(ListText));
				OnPropertyChanged(nameof(TrackingButtonText));
			}
		};
	}

	// Properties rather than x:Bind functions of Manga.Tracking: those aren't called again when it becomes null (removed).

	/// <summary>E.g. "Reading · 12 chapters read"; "" when it isn't on the list.</summary>
	public string ListText => Views.Manga.MangaDetailsPage.ListText(Manga.Tracking);

	public string TrackingButtonText => Views.Manga.MangaDetailsPage.TrackingButtonText(Manga.Tracking);

	public MangaModel Manga { get; }

	public IReadOnlyList<IMangaProvider> Sources { get; }

	public bool HasSources => Sources.Count > 0;

	/// <summary>Where chapters come from; the one in Settings to start with.</summary>
	[ObservableProperty]
	public partial IMangaProvider? Source { get; set; }

	/// <summary>The source's manga that is this one; null when it hasn't been found.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(MatchText))]
	public partial ProviderManga? Match { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(ReadText), nameof(CanRead))]
	public partial List<ProviderChapter> Chapters { get; set; } = [];

	/// <summary>Why there are no chapters (not found, none on the source...); "" when there are.</summary>
	[ObservableProperty]
	public partial string ChaptersMessage { get; set; } = "";

	/// <summary>Whether the source search (to pick the match by hand) is showing.</summary>
	[ObservableProperty]
	public partial bool IsPicking { get; set; }

	[ObservableProperty]
	public partial string SearchQuery { get; set; } = "";

	[ObservableProperty]
	public partial List<ProviderManga> SearchResults { get; set; } = [];

	public string MatchText => Match is null ? "" : $"On {Source?.Name}: {Match.Title}";

	public bool CanRead => Chapters.Count > 0;

	/// <summary>The main button: "Start reading", or "Continue: chapter 13" once some are read.</summary>
	public string ReadText => MangaReadingService.NextChapter(Chapters, Manga.Tracking) is { } next && Manga.Tracking?.ChaptersRead is > 0
		? $"Continue: chapter {next.Number}"
		: "Start reading";

	/// <summary>Under the title, as on the anime details page: its English (or romaji) title when that's not the title.</summary>
	public string Subtitle => new[] { Manga.EngTitle, Manga.RomajiTitle }.FirstOrDefault(x => !string.IsNullOrEmpty(x) && x != Manga.Title) ?? "";

	/// <summary>The Overview's chips, as the anime ones: e.g. Manhwa, Publishing, ★ 8.4, Since Dec 2018, 140 chapters, 12 volumes.</summary>
	public IReadOnlyList<string> Facts =>
	[
		.. new[]
		{
			Manga.Format == MangaFormat.Unknown ? null : Manga.Format.ToString(),
			Manga.Status switch
			{
				AiringStatus.CurrentlyAiring => "Publishing",
				AiringStatus.NotYetAired => "Not yet published",
				_ => "Finished",
			},
			Manga.MeanScore is > 0 and var score ? $"★ {score / 10:0.0}" : null,
			Manga.StartDate is { } start ? $"Since {start:MMM yyyy}" : null,
			Manga.TotalChapters is { } chapters ? $"{chapters} chapters" : null,
			Manga.TotalVolumes is { } volumes ? $"{volumes} volumes" : null,
		}.OfType<string>(),
	];

	protected override Task OnNavigatedToInternalAsync() => LoadChaptersAsync();

	partial void OnSourceChanged(IMangaProvider? value) => _ = LoadChaptersAsync();

	private async Task LoadChaptersAsync()
	{
		_load?.Cancel();
		var load = _load = new CancellationTokenSource();
		Match = null;
		Chapters = [];
		if (Source is not { } source)
		{
			ChaptersMessage = "Install a manga source from Plugins to read chapters.";
			return;
		}

		ChaptersMessage = $"Looking for it on {source.Name}…";
		using (BeginBusy())
		{
			try
			{
				var match = await _reading.FindAsync(source, Manga, load.Token);
				if (load.IsCancellationRequested)
				{
					return;
				}

				if (match is null)
				{
					ChaptersMessage = $"Couldn't find it on {source.Name}. Search for it there:";
					SearchQuery = Manga.Title;
					IsPicking = true;
					return;
				}

				await ShowAsync(source, match, load.Token);
			}
			catch (Exception ex) when (!load.IsCancellationRequested)
			{
				ChaptersMessage = $"Couldn't load its chapters from {source.Name}: {ex.Message}";
			}
		}
	}

	private async Task ShowAsync(IMangaProvider source, ProviderManga match, CancellationToken ct)
	{
		Match = match;
		var chapters = await MangaReadingService.GetChaptersAsync(source, match.Id, ct);
		if (ct.IsCancellationRequested)
		{
			return;
		}

		Chapters = chapters;
		// Licensed manga are often on a source without their chapters (MangaDex takes them down).
		ChaptersMessage = chapters.Count == 0 ? $"No chapters available from {source.Name}. Try another source." : "";
	}

	[RelayCommand]
	private void StartPicking()
	{
		SearchQuery = Manga.Title;
		IsPicking = true;
		_ = SearchAsync();
	}

	[RelayCommand]
	private async Task SearchAsync()
	{
		if (Source is { } source && SearchQuery.Trim().Length > 0)
		{
			try
			{
				SearchResults = await source.SearchAsync(SearchQuery.Trim(), CancellationToken.None);
			}
			catch (Exception ex)
			{
				ChaptersMessage = $"Couldn't search {source.Name}: {ex.Message}";
			}
		}
	}

	/// <summary>The user's pick of the source's manga: kept, and its chapters shown.</summary>
	[RelayCommand]
	private async Task PickAsync(ProviderManga match)
	{
		if (Source is not { } source)
		{
			return;
		}

		_reading.SaveMatch(source, Manga, match);
		IsPicking = false;
		SearchResults = [];
		_load?.Cancel();
		var load = _load = new CancellationTokenSource();
		using (BeginBusy())
		{
			try
			{
				await ShowAsync(source, match, load.Token);
			}
			catch (Exception ex) when (!load.IsCancellationRequested)
			{
				ChaptersMessage = $"Couldn't load its chapters from {source.Name}: {ex.Message}";
			}
		}
	}

	[RelayCommand]
	private Task ReadNextAsync()
	{
		return MangaReadingService.NextChapter(Chapters, Manga.Tracking) is { } next ? OpenChapterAsync(next) : Task.CompletedTask;
	}

	[RelayCommand]
	private Task OpenChapterAsync(ProviderChapter chapter)
	{
		return Source is { } source
			? MangaCommands.OpenReaderAsync(new ReaderRequest(Manga, source.Id, Chapters, Chapters.IndexOf(chapter)))
			: Task.CompletedTask;
	}
}
