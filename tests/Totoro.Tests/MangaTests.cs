using MalApi;
using Microsoft.Extensions.Logging.Abstractions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;
using Totoro.Core.Navigation;
using Totoro.Core.Services;
using Totoro.Modules.AnimeDatabase.Anilist;
using Totoro.Modules.AnimeDatabase.Anilist.GraphQl;
using Totoro.Modules.AnimeDatabase.MyAnimeList;
using Totoro.ViewModels.Manga;
using Xunit;

namespace Totoro.Tests;

public class MangaTests
{
	[Fact]
	public void ChapterRead_MovesProgressForwardOnly()
	{
		var manga = new MangaModel { Tracking = new MangaTracking { ChaptersRead = 10, Status = ListItemStatus.Watching, StartDate = new DateTime(2026, 1, 1) } };

		Assert.Null(MangaTrackingService.ChangesForChapterRead(manga, 9));
		Assert.Null(MangaTrackingService.ChangesForChapterRead(manga, 10));

		var changes = MangaTrackingService.ChangesForChapterRead(manga, 11.5f);
		Assert.Equal(11, changes?.ChaptersRead);
		Assert.Equal(ListItemStatus.Watching, changes?.Status);
		// Started already: left as it was.
		Assert.Null(changes?.StartDate);
	}

	[Fact]
	public void ChapterRead_CompletesAtTheLastChapter_AndStartsANewEntry()
	{
		var manga = new MangaModel { TotalChapters = 3 };

		var first = MangaTrackingService.ChangesForChapterRead(manga, 1);
		Assert.Equal(ListItemStatus.Watching, first?.Status);
		Assert.Equal(DateTime.Today, first?.StartDate);

		var last = MangaTrackingService.ChangesForChapterRead(manga, 3);
		Assert.Equal(ListItemStatus.Completed, last?.Status);
		Assert.Equal(DateTime.Today, last?.FinishDate);
	}

	[Fact]
	public void ChapterRead_KeepsRereading()
	{
		var manga = new MangaModel { TotalChapters = 100, Tracking = new MangaTracking { Status = ListItemStatus.Rewatching, ChaptersRead = 4 } };
		Assert.Equal(ListItemStatus.Rewatching, MangaTrackingService.ChangesForChapterRead(manga, 5)?.Status);
	}

	[Fact]
	public void NextChapter_IsTheFirstAfterThoseRead()
	{
		List<ProviderChapter> chapters = [new("m", "a", 1), new("m", "b", 2), new("m", "c", 2.5f), new("m", "d", 3)];

		Assert.Equal("a", MangaReadingService.NextChapter(chapters, null)?.Id);
		Assert.Equal("c", MangaReadingService.NextChapter(chapters, new MangaTracking { ChaptersRead = 2 })?.Id);
		// All read: start over.
		Assert.Equal("a", MangaReadingService.NextChapter(chapters, new MangaTracking { ChaptersRead = 3 })?.Id);
		Assert.Null(MangaReadingService.NextChapter([], null));
	}

	[Fact]
	public async Task PickedMatch_IsKeptPerSourceAndManga()
	{
		var source = new FakeSource();
		var preferences = new PluginLoaderTests.FakePreferences();
		var reading = new MangaReadingService(new Factory<IMangaProvider>(source), preferences);
		var manga = new MangaModel { Id = 1, Title = "Nothing like it" };

		Assert.Null(await reading.FindAsync(source, manga, CancellationToken.None));

		reading.SaveMatch(source, manga, new ProviderManga("picked", "The right one"));
		Assert.Equal("picked", (await reading.FindAsync(source, manga, CancellationToken.None))?.Id);
		Assert.Null(await reading.FindAsync(source, new MangaModel { Id = 2, Title = "Nothing like it" }, CancellationToken.None));
	}

	[Fact]
	public async Task TitleMatch_FindsAClose_TitleOrNothing()
	{
		var source = new FakeSource { Results = [new("x", "Chainsaw Man: Buddy Stories"), new("y", "Chainsaw Man")] };

		Assert.Equal("y", (await ((IMangaProvider)source).FindMangaAsync(new MangaModel { Title = "Chainsaw Man" }, CancellationToken.None))?.Id);
		Assert.Null(await ((IMangaProvider)source).FindMangaAsync(new MangaModel { Title = "Berserk" }, CancellationToken.None));
	}

	[Theory]
	[InlineData(MediaFormat.Manga, "JP", MangaFormat.Manga)]
	[InlineData(MediaFormat.Manga, "KR", MangaFormat.Manhwa)]
	[InlineData(MediaFormat.Manga, "CN", MangaFormat.Manhua)]
	[InlineData(MediaFormat.Novel, "JP", MangaFormat.Novel)]
	[InlineData(MediaFormat.OneShot, "JP", MangaFormat.OneShot)]
	public void Anilist_Format_TellsManhwaAndManhuaApart(MediaFormat format, string country, MangaFormat expected)
	{
		Assert.Equal(expected, AnilistMangaConverter.ConvertFormat(format, country));
	}

	[Fact]
	public void Anilist_Tracking_ReadsChaptersAndVolumes()
	{
		var tracking = AnilistMangaConverter.ConvertTracking(new MediaList { Status = MediaListStatus.Repeating, Progress = 12, ProgressVolumes = 2, Score = 8.5m });

		Assert.Equal(ListItemStatus.Rewatching, tracking?.Status);
		Assert.Equal(12, tracking?.ChaptersRead);
		Assert.Equal(2, tracking?.VolumesRead);
		Assert.Equal(8.5, tracking?.Score);
		Assert.Null(AnilistMangaConverter.ConvertTracking(null));
	}

	[Fact]
	public void Mal_Model_ConvertsCountsScoreAndRereading()
	{
		var manga = MalMangaConverter.ToModel(new Manga
		{
			Id = 116778,
			Title = "Chainsaw Man",
			TotalChapters = 0,
			TotalVolumes = 11,
			Mean = 8.6f,
			MediaType = MangaMediaType.Manhwa,
			UserStatus = new UserMangaStatus { Status = MangaStatus.Completed, IsReReading = true, ChaptersRead = 5 },
		});

		Assert.Equal(116778, manga.ExternalIds.MyAnimeList);
		// MAL says 0 for "not known yet".
		Assert.Null(manga.TotalChapters);
		Assert.Equal(11, manga.TotalVolumes);
		Assert.Equal(86, manga.MeanScore!.Value, 0.01);
		Assert.Equal(MangaFormat.Manhwa, manga.Format);
		Assert.Equal(ListItemStatus.Rewatching, manga.Tracking?.Status);
		Assert.Equal(MangaStatus.PlanToRead, MalMangaConverter.ConvertStatus(ListItemStatus.PlanToWatch));
	}

	[Fact]
	public void MangaList_TabsReadAsReading_AndFiltersAndSortsManga()
	{
		var tabs = new ListStatusFilter(MediaMode.Manga);
		Assert.Equal(["Reading", "Plan To Read", "Completed", "On Hold", "Dropped"], tabs.Options.Select(x => x.Label));
		Assert.Equal("Plan To Watch", new ListStatusFilter().Options[1].Label);

		var rereading = new MangaModel { Id = 1, Title = "Berserk", Tracking = new MangaTracking { Status = ListItemStatus.Rewatching, Score = 9 } };
		var planned = new MangaModel { Id = 2, Title = "Vagabond", Tracking = new MangaTracking { Status = ListItemStatus.PlanToWatch, Score = 7 } };
		Func<IMediaModel, bool>? reading = null;
		using (tabs.Predicate.Subscribe(x => reading = x))
		{
			// Rereading has no tab: it's under Reading.
			Assert.True(reading!(rereading));
			Assert.False(reading(planned));
		}

		Assert.True(UserListQuery.MatchesTitle(planned, "vaga"));
		Assert.Equal(["Berserk", "Vagabond"], UserListQuery.Sort<IMediaModel>([planned, rereading], ListSort.UserScore, descending: true).Select(x => x.Title));
		Assert.Equal("Rereading", MediaText.Status(ListItemStatus.Rewatching, MediaMode.Manga));
	}

	[Fact]
	public void Spreads_PairPagesAfterTheCover_AndPutTheEarlierOnTheRightReadingRightToLeft()
	{
		List<ReaderPageItem> pages = [.. Enumerable.Range(1, 6).Select(n => new ReaderPageItem(new MangaPage(new Uri($"http://x/{n}")), n, 6))];

		Assert.Equal(6, ReaderSpread.Of(pages, twoPages: false, rightToLeft: false).Count);

		var spreads = ReaderSpread.Of(pages, twoPages: true, rightToLeft: false);
		// 1 alone, 2-3, 4-5, and 6 alone at the end.
		Assert.Equal([[1], [2, 3], [4, 5], [6]], spreads.Select(x => x.Pages.Select(p => p.Number).ToArray()));
		Assert.Equal((2, 3), (spreads[1].Left.Number, spreads[1].Right!.Number));
		Assert.True(spreads[0].IsSingle);

		var rightToLeft = ReaderSpread.Of(pages, twoPages: true, rightToLeft: true);
		Assert.Equal((3, 2), (rightToLeft[1].Left.Number, rightToLeft[1].Right!.Number));
	}

	[Fact]
	public void Spreads_LeaveWidePagesAlone_AndThePageBeforeOne()
	{
		List<ReaderPageItem> pages = [.. Enumerable.Range(1, 8).Select(n => new ReaderPageItem(new MangaPage(new Uri($"http://x/{n}")), n, 8))];
		pages[4].AsWide();

		var spreads = ReaderSpread.Of(pages, twoPages: true, rightToLeft: false);
		Assert.Equal([[1], [2, 3], [4], [5], [6, 7], [8]], spreads.Select(x => x.Pages.Select(p => p.Number).ToArray()));
	}

	[Fact]
	public async Task Reader_SwitchingToTwoPages_KeepsThePage()
	{
		var source = new FakeSource();
		List<ProviderChapter> chapters = [new("m", "c1", 1)];
		var reader = new ReaderViewModel(new ReaderRequest(null, source.Id, chapters, 0),
			new MangaReadingService(new Factory<IMangaProvider>(source), new PluginLoaderTests.FakePreferences()),
			new MangaTrackingService(new Factory<IMangaTracker>(new FakeTracker())),
			new PluginLoaderTests.FakePreferences(), new HttpClients(), NullLogger<ReaderViewModel>.Instance) { Mode = ReaderMode.PagedLeftToRight };
		await reader.OnNavigatedToAsync();
		reader.NextPageCommand.Execute(null);

		// What the flip view does when its items change: back to the first.
		reader.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(ReaderViewModel.Spreads))
			{
				reader.SpreadIndex = 0;
			}
		};
		reader.TwoPages = true;

		Assert.Equal(1, reader.PageIndex);
		Assert.Equal(1, reader.SpreadIndex);
	}

	[Fact]
	public void NavigationItem_NamesMangaPagesApart()
	{
		var anime = new NavigationItemViewModel { ViewModelType = typeof(object), Label = "Library", Mode = MediaMode.Anime };
		var manga = new NavigationItemViewModel { ViewModelType = typeof(object), Label = "Library", Mode = MediaMode.Manga };
		Assert.Equal("Library", anime.Name);
		Assert.Equal("Library (manga)", manga.Name);
	}

	[Fact]
	public async Task Reader_TurningPastTheLastPage_MarksReadAndOpensTheNextChapter()
	{
		var source = new FakeSource();
		var tracker = new FakeTracker();
		var manga = new MangaModel { Id = 7, Title = "Test", TotalChapters = 10 };
		List<ProviderChapter> chapters = [new("m", "c1", 1), new("m", "c2", 2)];
		var reader = new ReaderViewModel(new ReaderRequest(manga, source.Id, chapters, 0),
			new MangaReadingService(new Factory<IMangaProvider>(source), new PluginLoaderTests.FakePreferences()),
			new MangaTrackingService(new Factory<IMangaTracker>(tracker)),
			new PluginLoaderTests.FakePreferences(), new HttpClients(), NullLogger<ReaderViewModel>.Instance);

		await reader.OnNavigatedToAsync();
		Assert.Equal(2, reader.PageCount);
		Assert.False(reader.HasPrevious);

		reader.NextPageCommand.Execute(null);
		Assert.Equal(1, reader.PageIndex);
		await Task.Delay(50);
		Assert.Equal(1, tracker.Saved?.ChaptersRead);

		reader.NextPageCommand.Execute(null);
		await Task.Delay(50);
		Assert.Equal("c2", reader.Chapter.Id);
		Assert.Equal(0, reader.PageIndex);

		// Back from the first page goes to the previous chapter's last page.
		reader.PreviousPageCommand.Execute(null);
		await Task.Delay(50);
		Assert.Equal("c1", reader.Chapter.Id);
		Assert.Equal(1, reader.PageIndex);
	}

	[Fact]
	public async Task Reader_SwitchingChapterOrLeaving_WhilePagesLoad_DoesNotThrow()
	{
		// A server that takes connections and never answers: the pages are still loading when the chapter changes.
		var server = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
		server.Start();
		var port = ((System.Net.IPEndPoint)server.LocalEndpoint).Port;
		var source = new FakeSource { PageUrl = $"http://127.0.0.1:{port}/" };
		List<ProviderChapter> chapters = [new("m", "c1", 1), new("m", "c2", 2)];
		var reader = new ReaderViewModel(new ReaderRequest(null, source.Id, chapters, 0),
			new MangaReadingService(new Factory<IMangaProvider>(source), new PluginLoaderTests.FakePreferences()),
			new MangaTrackingService(new Factory<IMangaTracker>(new FakeTracker())),
			new PluginLoaderTests.FakePreferences(), new HttpClients(), NullLogger<ReaderViewModel>.Instance);

		try
		{
			var first = reader.OnNavigatedToAsync();
			await Task.Delay(200);
			Assert.False(first.IsCompleted);

			var next = reader.NextChapterCommand.ExecuteAsync(null);
			await first.WaitAsync(TimeSpan.FromSeconds(5));
			Assert.Equal("c2", reader.Chapter.Id);

			await reader.OnNavigatedFromAsync();
			await next.WaitAsync(TimeSpan.FromSeconds(5));
		}
		finally
		{
			server.Stop();
		}
	}

	private sealed class FakeSource : IMangaProvider
	{
		public string PageUrl { get; init; } = "http://127.0.0.1:9/";
		public List<ProviderManga> Results { get; init; } = [];
		public Guid Id { get; } = Guid.NewGuid();
		public string Name => "Fake";
		public Task<List<ProviderManga>> SearchAsync(string query, CancellationToken ct) => Task.FromResult(Results);
		public Task<List<ProviderChapter>> GetChaptersAsync(string mangaId, CancellationToken ct) => Task.FromResult<List<ProviderChapter>>([]);

		// Nowhere to load from: the pages fail, which the reader shows with Retry.
		public Task<List<MangaPage>> GetPagesAsync(ProviderChapter chapter, CancellationToken ct) =>
			Task.FromResult<List<MangaPage>>([new(new Uri(PageUrl + "1.png")), new(new Uri(PageUrl + "2.png"))]);
	}

	private sealed class FakeTracker : IMangaTracker
	{
		public MangaTracking? Saved { get; private set; }
		public Guid Id { get; } = Guid.NewGuid();
		public string Name => "Fake";
		public Task<List<MangaModel>> GetUserListAsync(CancellationToken ct) => Task.FromResult<List<MangaModel>>([]);
		public Task<bool> RemoveAsync(long id) => Task.FromResult(true);

		public Task<MangaTracking> UpdateAsync(long id, MangaTracking tracking)
		{
			Saved = tracking;
			return Task.FromResult(tracking);
		}
	}

	private sealed class Factory<T>(T item) : IFactory<T, Guid> where T : notnull
	{
		public T? Create(Guid id) => item;
		public T? CreateDefault() => item;
		public IEnumerable<T> CreateAll() => [item];
		public Guid GetDefaultId() => Guid.Empty;
	}

	private sealed class HttpClients : IHttpClientFactory
	{
		public HttpClient CreateClient(string name) => new();
	}
}
