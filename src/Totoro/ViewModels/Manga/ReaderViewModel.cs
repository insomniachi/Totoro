using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media.Imaging;
using Totoro.Core.Modules.Manga;
using Totoro.Core.Services;
using Totoro.Core.Settings;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels.Manga;

/// <summary>What the reader opens: a chapter of a manga on a source, with the others to go on to.</summary>
/// <param name="Manga">The database's manga, whose progress reading updates; null when read straight from a source.</param>
/// <param name="Chapters">The manga's chapters, in reading order.</param>
/// <param name="Index">The chapter to open, in <paramref name="Chapters"/>.</param>
public sealed record ReaderRequest(MangaModel? Manga, Guid SourceId, IReadOnlyList<ProviderChapter> Chapters, int Index);

/// <summary>
/// Reads a chapter, a page at a time or scrolling, and goes on to the next. Finishing a chapter (reaching its last
/// page) marks it read on the user's list.
/// </summary>
public partial class ReaderViewModel : NavigatableViewModel
{
	// Pages loaded at once: the next ones are ready by the time they're turned to.
	private const int ConcurrentLoads = 3;

	private readonly ReaderRequest _request;
	private readonly IMangaProvider? _source;
	private readonly MangaTrackingService _tracking;
	private readonly IPreferences _preferences;
	private readonly HttpClient _http;
	private readonly ILogger<ReaderViewModel> _logger;
	private CancellationTokenSource? _load;
	private bool _markedRead;

	public ReaderViewModel(ReaderRequest request, MangaReadingService reading, MangaTrackingService tracking, IPreferences preferences,
		IHttpClientFactory httpClients, ILogger<ReaderViewModel> logger)
	{
		_request = request;
		_source = reading.GetSource(request.SourceId);
		_tracking = tracking;
		_preferences = preferences;
		_http = httpClients.CreateClient(nameof(ReaderViewModel));
		_logger = logger;
		ChapterIndex = request.Index;
		Mode = preferences.ReaderMode;
		TwoPages = preferences.ReaderTwoPages;
	}

	public string MangaTitle => _request.Manga?.Title ?? "";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(Chapter), nameof(ChapterTitle), nameof(HasPrevious), nameof(HasNext))]
	public partial int ChapterIndex { get; set; }

	public ProviderChapter Chapter => _request.Chapters[ChapterIndex];

	public string ChapterTitle => Views.Manga.MangaDetailsPage.ChapterText(Chapter.Number, Chapter.Title);

	public bool HasPrevious => ChapterIndex > 0;

	public bool HasNext => ChapterIndex < _request.Chapters.Count - 1;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PageCount), nameof(LastPage), nameof(PageText))]
	public partial List<ReaderPageItem> Pages { get; set; } = [];

	/// <summary>Whether a page at a time is two, side by side (see <see cref="ReaderSpread"/>).</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PageText))]
	public partial bool TwoPages { get; set; }

	/// <summary>What a page at a time shows, one after another: a page, or two side by side.</summary>
	[ObservableProperty]
	public partial List<ReaderSpread> Spreads { get; private set; } = [];

	/// <summary>The spread showing, which <see cref="PageIndex"/> follows (its last page) and the other way round.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PageText))]
	public partial int SpreadIndex { get; set; }

	// Set while one of PageIndex and SpreadIndex follows the other, so it doesn't follow back.
	private bool _syncing;

	partial void OnPagesChanged(List<ReaderPageItem> value) => UpdateSpreads();

	partial void OnTwoPagesChanged(bool value)
	{
		_preferences.ReaderTwoPages = value;
		UpdateSpreads();
	}

	private void UpdateSpreads()
	{
		// Re-paired (a wide page found), the spread showing starts where the old one did, not at the page it ended on.
		var anchor = Spreads.ElementAtOrDefault(SpreadIndex) is { } shown && Pages.Contains(shown.Pages[0]) ? shown.Pages[0].Number - 1 : PageIndex;
		// The flip view goes back to its first spread when given new ones, which mustn't turn back the page being read.
		_syncing = true;
		Spreads = ReaderSpread.Of(Pages, TwoPages, IsRightToLeft);
		_syncing = false;
		var spread = Spreads.FindIndex(x => x.Pages.Any(page => page.Number - 1 == anchor));
		if (spread >= 0 && spread != SpreadIndex)
		{
			SpreadIndex = spread;
		}
		else
		{
			FollowPage();
		}
	}

	partial void OnSpreadIndexChanged(int value)
	{
		if (_syncing || value < 0 || value >= Spreads.Count)
		{
			return;
		}

		_syncing = true;
		PageIndex = Spreads[value].Pages[^1].Number - 1;
		_syncing = false;
	}

	/// <summary>Shows the spread with the page being read.</summary>
	private void FollowPage()
	{
		var spread = Spreads.FindIndex(x => x.Pages.Any(page => page.Number - 1 == PageIndex));
		if (_syncing || spread < 0 || spread == SpreadIndex)
		{
			return;
		}

		_syncing = true;
		SpreadIndex = spread;
		_syncing = false;
	}

	public int PageCount => Pages.Count;

	/// <summary>The last page's index, for the page slider's maximum.</summary>
	public int LastPage => Math.Max(0, Pages.Count - 1);

	/// <summary>The page being read, from 0.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(PageText), nameof(PageNumber))]
	public partial int PageIndex { get; set; }

	/// <summary><see cref="PageIndex"/> from 1, for the page slider.</summary>
	public double PageNumber
	{
		get => PageIndex + 1;
		set => PageIndex = Math.Clamp((int)Math.Round(value) - 1, 0, LastPage);
	}

	public string PageText => Pages.Count == 0 ? ""
		: Spreads.ElementAtOrDefault(SpreadIndex) is { Pages: [var first, .., var last] } ? $"Pages {first.Number}–{last.Number} of {Pages.Count}"
		: $"Page {PageIndex + 1} of {Pages.Count}";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsRightToLeft))]
	public partial ReaderMode Mode { get; set; }

	/// <summary>Whether pages turn right to left, as most manga read; <see cref="Mode"/> as the reader's toggle.</summary>
	public bool IsRightToLeft
	{
		get => Mode == ReaderMode.PagedRightToLeft;
		set => Mode = value ? ReaderMode.PagedRightToLeft : ReaderMode.PagedLeftToRight;
	}

	/// <summary>Why the chapter didn't open; "" if it did.</summary>
	[ObservableProperty]
	public partial string Error { get; set; } = "";

	partial void OnModeChanged(ReaderMode value)
	{
		_preferences.ReaderMode = value;
		// Two pages read right to left are shown the other way round.
		UpdateSpreads();
		OnPropertyChanged(nameof(PageText));
	}

	partial void OnPageIndexChanged(int value)
	{
		FollowPage();
		if (Pages.Count > 0 && value >= Pages.Count - 1)
		{
			_ = MarkReadAsync();
		}
	}

	protected override Task OnNavigatedToInternalAsync() => LoadChapterAsync();

	protected override Task OnNavigatedFromInternalAsync()
	{
		_load?.Cancel();
		return Task.CompletedTask;
	}

	[RelayCommand]
	private Task LoadChapterAsync() => LoadChapterAsync(false);

	private async Task LoadChapterAsync(bool fromEnd)
	{
		_load?.Cancel();
		var load = _load = new CancellationTokenSource();
		_markedRead = false;
		Error = "";
		Pages = [];
		PageIndex = 0;
		if (_source is null)
		{
			Error = "The manga source this chapter is from isn't installed any more.";
			return;
		}

		using (BeginBusy())
		{
			try
			{
				var pages = await _source.GetPagesAsync(Chapter, load.Token);
				if (load.IsCancellationRequested)
				{
					return;
				}

				Pages = [.. pages.Select((x, i) => new ReaderPageItem(x, i + 1, pages.Count))];
				PageIndex = fromEnd ? LastPage : 0;
				if (Pages.Count == 0)
				{
					Error = "This chapter has no pages on the source.";
				}
			}
			catch (OperationCanceledException) when (load.IsCancellationRequested)
			{
				// Another chapter was opened, or the reader closed, meanwhile.
				return;
			}
			catch (Exception ex) when (!load.IsCancellationRequested)
			{
				_logger.LogWarning(ex, "Couldn't open {Chapter}", ChapterTitle);
				Error = $"Couldn't open the chapter: {ex.Message}";
				return;
			}
		}

		try
		{
			await LoadPagesAsync(Pages, load.Token);
		}
		catch (OperationCanceledException) when (load.IsCancellationRequested)
		{
			// As above: its pages aren't wanted any more. Thrown, it would reach the command, which rethrows it on the UI thread.
		}
	}

	// shortcut: every page of the chapter is decoded and kept while it's open (fine for ~100 pages); unload far-off pages if long webtoons use too much memory.
	private async Task LoadPagesAsync(List<ReaderPageItem> pages, CancellationToken ct)
	{
		using var slots = new SemaphoreSlim(ConcurrentLoads);
		await Task.WhenAll(pages.Select(async page =>
		{
			await slots.WaitAsync(ct);
			try
			{
				await LoadPageAsync(page, ct);
			}
			finally
			{
				slots.Release();
			}
		}));
	}

	private async Task LoadPageAsync(ReaderPageItem page, CancellationToken ct)
	{
		page.IsFailed = false;
		for (var attempt = 0; attempt < 2 && !ct.IsCancellationRequested; attempt++)
		{
			try
			{
				// A second try asks the source for the page again: some (MangaDex) hand out addresses that stop working.
				var url = page.Page.Url;
				if (attempt > 0 && _source is not null && (await _source.GetPagesAsync(Chapter, ct)).ElementAtOrDefault(page.Number - 1) is { } fresh)
				{
					url = fresh.Url;
				}

				using var request = new HttpRequestMessage(HttpMethod.Get, url);
				foreach (var (name, value) in page.Page.Headers)
				{
					request.Headers.TryAddWithoutValidation(name, value);
				}

				using var response = await _http.SendAsync(request, ct);
				response.EnsureSuccessStatusCode();
				var bytes = await response.Content.ReadAsByteArrayAsync(ct);
				var image = new BitmapImage();
				using var stream = new MemoryStream(bytes);
				await image.SetSourceAsync(stream.AsRandomAccessStream());
				page.Image = image;
				// Two pages in one image stand alone, two at a time: they're paired again with it known.
				if (page.IsWide && TwoPages)
				{
					UpdateSpreads();
				}

				return;
			}
			catch (Exception ex) when (!ct.IsCancellationRequested)
			{
				_logger.LogInformation(ex, "Page {Page} didn't load (try {Attempt})", page.Number, attempt + 1);
			}
		}

		page.IsFailed = !ct.IsCancellationRequested;
	}

	[RelayCommand]
	private async Task RetryPageAsync(ReaderPageItem page)
	{
		var ct = _load?.Token ?? CancellationToken.None;
		try
		{
			await LoadPageAsync(page, ct);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
		}
	}

	/// <summary>Finishing the chapter marks it read (once), if it's from the user's database.</summary>
	private async Task MarkReadAsync()
	{
		if (_markedRead || _request.Manga is not { } manga)
		{
			return;
		}

		_markedRead = true;
		try
		{
			await _tracking.MarkChapterReadAsync(manga, Chapter.Number);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Couldn't mark {Chapter} of {Manga} read", ChapterTitle, manga.Title);
		}
	}

	/// <summary>The next page (or spread, a page at a time), or the next chapter after the last.</summary>
	[RelayCommand]
	private void NextPage()
	{
		if (SpreadIndex < Spreads.Count - 1)
		{
			SpreadIndex++;
		}
		else if (HasNext)
		{
			_ = NextChapterAsync();
		}
	}

	/// <summary>The previous page (or spread), or the previous chapter's last before the first.</summary>
	[RelayCommand]
	private void PreviousPage()
	{
		if (SpreadIndex > 0)
		{
			SpreadIndex--;
		}
		else if (HasPrevious)
		{
			ChapterIndex--;
			_ = LoadChapterAsync(fromEnd: true);
		}
	}

	[RelayCommand]
	private Task NextChapterAsync()
	{
		if (!HasNext)
		{
			return Task.CompletedTask;
		}

		ChapterIndex++;
		return LoadChapterAsync(false);
	}

	[RelayCommand]
	private Task PreviousChapterAsync()
	{
		if (!HasPrevious)
		{
			return Task.CompletedTask;
		}

		ChapterIndex--;
		return LoadChapterAsync(false);
	}
}

/// <summary>
/// What the reader shows at once a page at a time: a page, or two side by side. Two at a time, the first page (the
/// cover) is alone, so the pages after it pair up as they were printed, facing each other; a wide page (two facing
/// pages drawn as one) is alone too, as it's both.
/// </summary>
/// <param name="Pages">In reading order.</param>
/// <param name="Left">The page on the left: the later one, read right to left.</param>
/// <param name="Right">The other page; null when the spread is one page.</param>
public sealed record ReaderSpread(IReadOnlyList<ReaderPageItem> Pages, ReaderPageItem Left, ReaderPageItem? Right)
{
	public bool IsSingle => Right is null;

	public static List<ReaderSpread> Of(IReadOnlyList<ReaderPageItem> pages, bool twoPages, bool rightToLeft)
	{
		if (!twoPages)
		{
			return [.. pages.Select(x => new ReaderSpread([x], x, null))];
		}

		List<ReaderSpread> spreads = [];
		for (var i = 0; i < pages.Count;)
		{
			// The cover, the last page, and wide pages (two pages in one image) are alone; so is a page before a wide one.
			if (i == 0 || i == pages.Count - 1 || pages[i].IsWide || pages[i + 1].IsWide)
			{
				spreads.Add(new([pages[i]], pages[i], null));
				i++;
				continue;
			}

			var (first, second) = (pages[i], pages[i + 1]);
			spreads.Add(rightToLeft ? new([first, second], second, first) : new([first, second], first, second));
			i += 2;
		}

		return spreads;
	}
}

/// <summary>A page in the reader: its image once loaded, or that it failed to.</summary>
public partial class ReaderPageItem(MangaPage page, int number, int count) : ObservableObject
{
	public MangaPage Page { get; } = page;

	/// <summary>From 1.</summary>
	public int Number { get; } = number;

	/// <summary>"Page 3 of 20", for screen readers.</summary>
	public string Name { get; } = $"Page {number} of {count}";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsLoading), nameof(IsWide))]
	public partial BitmapImage? Image { get; set; }

	/// <summary>Whether it's wider than tall, as two facing pages drawn as one are; false until its image has loaded.</summary>
	public bool IsWide => Image is { PixelWidth: > 0 } image ? image.PixelWidth > image.PixelHeight : _wide;

	// For tests, which can't make images.
	private bool _wide;

	internal ReaderPageItem AsWide()
	{
		_wide = true;
		return this;
	}

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsLoading))]
	public partial bool IsFailed { get; set; }

	public bool IsLoading => Image is null && !IsFailed;
}
