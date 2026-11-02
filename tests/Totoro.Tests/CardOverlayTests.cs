using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Manga;
using Totoro.UI.Controls.AnimeCardOverlays;
using Xunit;

namespace Totoro.Tests;

/// <summary>Which overlays a card gets (what the CardOverlaySelectors ask), for anime and manga.</summary>
public class CardOverlayTests
{
	private static AnimeModel Anime(AiringStatus airing, ListItemStatus? status = null) => new()
	{
		AiringStatus = airing,
		Tracking = status is null ? null : new Tracking { Status = status },
	};

	private static MangaModel Manga(AiringStatus publishing, ListItemStatus? status = null) => new()
	{
		Status = publishing,
		Tracking = status is null ? null : new MangaTracking { Status = status },
	};

	[Theory]
	[InlineData(AiringStatus.CurrentlyAiring, null, true)]
	[InlineData(AiringStatus.NotYetAired, ListItemStatus.PlanToWatch, true)]
	[InlineData(AiringStatus.CurrentlyAiring, ListItemStatus.Watching, false)] // its countdown says it's airing
	[InlineData(AiringStatus.FinishedAiring, null, false)]
	public void AiringStatus_AnimeAndManga(AiringStatus airing, ListItemStatus? status, bool shown)
	{
		Assert.Equal(shown, AiringStatusOverlay.IsShown(Anime(airing, status)));
		Assert.Equal(shown, AiringStatusOverlay.IsShown(Manga(airing, status)));
	}

	[Fact]
	public void FormatSeason_Manga_IsFormatAndYear()
	{
		var manga = new MangaModel { Format = MangaFormat.Manhwa, StartDate = new DateTime(2018, 12, 1) };
		Assert.Equal("Manhwa · 2018", FormatSeasonOverlay.Text(manga));
		Assert.False(FormatSeasonOverlay.IsShown(new MangaModel()));
	}

	[Fact]
	public void MangaFormat_ReadsItsDescription()
	{
		Assert.Equal("Light novel", MediaText.Format(MangaFormat.Novel));
		Assert.Null(MediaText.Format(MangaFormat.Unknown));
	}

	[Fact]
	public void MeanScore_NoneAtZero()
	{
		Assert.False(MeanScoreOverlay.IsShown(new MangaModel { MeanScore = 0 }));
		Assert.True(MeanScoreOverlay.IsShown(new MangaModel { MeanScore = 84 }));
	}

	[Theory]
	[InlineData(ListItemStatus.Watching, true)]
	[InlineData(ListItemStatus.Rewatching, true)]
	[InlineData(ListItemStatus.Completed, false)]
	public void WatchProgress_OnlyWhileWatchingOrReading(ListItemStatus status, bool shown)
	{
		Assert.Equal(shown, WatchProgressOverlay.IsShown(Anime(AiringStatus.FinishedAiring, status)));
		Assert.Equal(shown, WatchProgressOverlay.IsShown(Manga(AiringStatus.FinishedAiring, status)));
	}

	[Fact]
	public void NextEpisodeAndNewEpisodes_NeverForManga()
	{
		var manga = Manga(AiringStatus.CurrentlyAiring, ListItemStatus.Watching);
		Assert.False(NextEpisodeOverlay.IsShown(manga, DateTime.Now));
		Assert.False(NewEpisodesOverlay.IsShown(manga, DateTime.Now));
	}
}
