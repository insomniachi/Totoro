using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Core;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeDatabase.Anilist;
using Totoro.Modules.AnimeDatabase.Anilist.GraphQl;
using Xunit;
using AnilistSettings = Totoro.Modules.AnimeDatabase.Anilist.Settings;

namespace Totoro.Tests;

public class AnilistConverterTests
{
	/// <summary>
	/// The converter reads AniList's settings through <see cref="Locator"/> when it's first used, and a failure there would
	/// break it for the whole run: so the settings are there before any test runs, whichever uses it first.
	/// </summary>
	[ModuleInitializer]
	internal static void ProvideAnilistSettings()
	{
		Locator.SetServiceProvider(new ServiceCollection()
			.AddSingleton<IModuleSettings<AnilistSettings>>(new FakeSettings())
			.BuildServiceProvider());
	}

	private static Media Media(int id = 1, MediaType type = MediaType.Anime) => new()
	{
		Id = id,
		IdMal = id + 1000,
		Type = type,
		Title = new MediaTitle { Romaji = "Sousou no Frieren", English = "Frieren", Native = "葬送のフリーレン" },
		CoverImage = new MediaCoverImage { Large = "cover.png" },
		Genres = ["Adventure"],
		Status = MediaStatus.Releasing,
		Format = MediaFormat.Tv,
		Season = MediaSeason.Fall,
		SeasonYear = 2023,
		Popularity = 500,
	};

	[Fact]
	public void ConvertModel_MapsTheMedia()
	{
		var media = Media(154587);
		media.Synonyms = ["Frieren: Beyond Journey's End", "", "Frieren"];
		media.EndDate = new FuzzyDate { Year = 2024, Month = 3, Day = 22 };
		media.NextAiringEpisode = new AiringSchedule { Episode = 5, TimeUntilAiring = 3600 };
		media.Trailer = new MediaTrailer { Site = "YouTube", Id = "abc", Thumbnail = "thumb.jpg" };
		media.Studios = new StudioConnection { Nodes = [new Studio { Name = "Madhouse", IsAnimationStudio = true }, new Studio { Name = "Aniplex", IsAnimationStudio = false }, new Studio { Name = "Madhouse", IsAnimationStudio = true }] };
		media.ExternalLinks =
		[
			new MediaExternalLink { Type = ExternalLinkType.Streaming, Site = "Crunchyroll", Url = "https://cr/1", Language = "" },
			new MediaExternalLink { Type = ExternalLinkType.Streaming, Site = "Crunchyroll", Url = "https://cr/1" },
			new MediaExternalLink { Type = ExternalLinkType.Info, Site = "Official", Url = "https://official" },
		];
		media.AiringSchedule = new AiringScheduleConnection
		{
			Nodes = [new AiringSchedule { Episode = 1, AiringAt = 1_700_000_000 }, new AiringSchedule { Episode = 1, AiringAt = 1_700_000_001 }, new AiringSchedule { Episode = null, AiringAt = 1 }],
		};

		var anime = AniListModelToAnimeModelConverter.ConvertModel(media);

		// Romaji by default (AniList's settings).
		Assert.Equal("Sousou no Frieren", anime.Title);
		Assert.Equal("Frieren", anime.EngTitle);
		Assert.Equal((154587L, 155587L), (anime.ExternalIds.Anilist, anime.ExternalIds.MyAnimeList));
		Assert.Equal("cover.png", anime.BannerImage);
		Assert.Equal(AiringStatus.CurrentlyAiring, anime.AiringStatus);
		Assert.Equal(4, anime.AiredEpisodes);
		Assert.NotNull(anime.NextEpisodeAt);
		Assert.Equal(new DateTime(2024, 3, 22), anime.EndDate);
		Assert.Equal(new Season(AnimeSeason.Fall, 2023), anime.Season);
		Assert.Equal(AnimeMediaFormat.Tv, anime.MediaFormat);
		Assert.Equal(nameof(AnimeId.Anilist), anime.ServiceName);
		Assert.Equal("https://anilist.co/anime/154587/", anime.Url);
		Assert.Equal(["Madhouse"], anime.Studios);
		Assert.Equal("https://www.youtube.com/watch?v=abc", Assert.Single(anime.Trailers).Url);
		var link = Assert.Single(anime.StreamingLinks);
		Assert.Null(link.Language);
		Assert.Equal([1], anime.AiringSchedule.Keys);
		Assert.Equal(["Frieren", "Sousou no Frieren", "葬送のフリーレン", "Frieren: Beyond Journey's End"], anime.AlternateTitles);
		Assert.Empty(anime.Episodes);
		Assert.Null(anime.Tracking);
	}

	[Fact]
	public void ConvertModel_EpisodesFromStreamingEpisodes_UpToTheNextToAir()
	{
		var media = Media();
		media.StreamingEpisodes = [new MediaStreamingEpisode { Title = "Episode 1", Thumbnail = "1.jpg" }];
		media.AiringSchedule = new AiringScheduleConnection
		{
			Nodes = [new AiringSchedule { Episode = 1 }, null!, new AiringSchedule { Episode = 2 }, new AiringSchedule { Episode = 3, TimeUntilAiring = 60 }, new AiringSchedule { Episode = 4 }],
		};

		var episodes = AniListModelToAnimeModelConverter.ConvertModel(media).Episodes;

		Assert.Equal([1, 2], episodes.Select(x => x.EpisodeNumber));
		Assert.Equal("Episode 1", episodes[0].Titles.English);
		Assert.Equal("1.jpg", episodes[0].Image);
	}

	[Fact]
	public void ConvertSimple_KeepsOnlyAnime()
	{
		Assert.Empty(AniListModelToAnimeModelConverter.ConvertSimple(null));
		Assert.Equal([1], AniListModelToAnimeModelConverter.ConvertSimple([Media(1), Media(2, MediaType.Manga)]).Select(x => x.Id));
	}

	[Fact]
	public void ConvertRelated_KeepsAnime_WithHowTheyRelate()
	{
		var related = AniListModelToAnimeModelConverter.ConvertRelated(
		[
			new MediaEdge { Node = Media(1), RelationType = MediaRelation.Sequel },
			new MediaEdge { Node = Media(2, MediaType.Manga), RelationType = MediaRelation.Adaptation },
			new MediaEdge { Node = Media(3), RelationType = MediaRelation.Character },
		]);

		Assert.Equal([(1L, (AnimeRelationType?)AnimeRelationType.Sequel), (3L, AnimeRelationType.Other)], related.Select(x => (x.Id, x.Relation)));
		Assert.Empty(AniListModelToAnimeModelConverter.ConvertRelated(null));
	}

	[Fact]
	public void ConvertTracking_MapsTheListEntry()
	{
		Assert.Null(AniListModelToAnimeModelConverter.ConvertTracking(null));

		var tracking = AniListModelToAnimeModelConverter.ConvertTracking(new MediaList
		{
			Status = MediaListStatus.Repeating,
			Progress = 3,
			Score = 8.5m,
			StartedAt = new FuzzyDate { Year = 2024, Month = 1, Day = 2 },
			CompletedAt = new FuzzyDate { Year = 2024, Month = 1 },
			UpdatedAt = 0,
		})!;

		Assert.Equal(ListItemStatus.Rewatching, tracking.Status);
		Assert.Equal(3, tracking.WatchedEpisodes);
		Assert.Equal(8.5, tracking.Score);
		Assert.Equal(new DateTime(2024, 1, 2), tracking.StartDate);
		Assert.Null(tracking.FinishDate);
		Assert.Null(tracking.UpdatedAt);

		var empty = AniListModelToAnimeModelConverter.ConvertTracking(new MediaList { Progress = 0, Score = 0 })!;
		Assert.Null(empty.WatchedEpisodes);
		Assert.Null(empty.Score);
		Assert.Null(empty.Status);
	}

	[Theory]
	[InlineData(ListItemStatus.Watching)]
	[InlineData(ListItemStatus.PlanToWatch)]
	[InlineData(ListItemStatus.OnHold)]
	[InlineData(ListItemStatus.Completed)]
	[InlineData(ListItemStatus.Dropped)]
	[InlineData(ListItemStatus.Rewatching)]
	public void ConvertListStatus_RoundTrips(ListItemStatus status)
	{
		Assert.Equal(status, AniListModelToAnimeModelConverter.ConvertListStatus(AniListModelToAnimeModelConverter.ConvertListStatus(status)));
	}

	[Theory]
	[InlineData(AnimeSeason.Winter)]
	[InlineData(AnimeSeason.Spring)]
	[InlineData(AnimeSeason.Summer)]
	[InlineData(AnimeSeason.Fall)]
	public void ConvertSeason_RoundTrips(AnimeSeason season)
	{
		Assert.Equal(season, AniListModelToAnimeModelConverter.ConvertSeason(AniListModelToAnimeModelConverter.ConvertSeason(season)!.Value));
	}

	[Fact]
	public void SmallConversions()
	{
		Assert.Equal(AiringStatus.FinishedAiring, AniListModelToAnimeModelConverter.ConvertStatus(MediaStatus.Finished));
		Assert.Equal(AiringStatus.NotYetAired, AniListModelToAnimeModelConverter.ConvertStatus(null));
		Assert.Null(AniListModelToAnimeModelConverter.ConvertToExactTime(null));
		Assert.Null(AniListModelToAnimeModelConverter.ConvertRelation(null));
		Assert.Null(AniListModelToAnimeModelConverter.ConvertDate((DateTime?)null));
		var date = AniListModelToAnimeModelConverter.ConvertDate(new DateTime(2025, 4, 6))!;
		Assert.Equal((2025, 4, 6), (date.Year.Value, date.Month.Value, date.Day.Value));
		Assert.Null(AniListModelToAnimeModelConverter.ConvertSeason((AnimeSeason?)null));
		Assert.Equal(MediaSource.LightNovel, AniListModelToAnimeModelConverter.ConvertSource(AnimeSource.LightNovel));
		Assert.Null(AniListModelToAnimeModelConverter.ConvertSource(null));
	}

	private sealed class FakeSettings : IModuleSettings<AnilistSettings>
	{
		public AnilistSettings Value { get; } = new();
		public DataContainerConfig Config => Value;
		public Descriptor Descriptor => throw new NotSupportedException();
		public DataContainerConfig CreateCopy(JsonObject? changes = null) => throw new NotSupportedException();
		public JsonObject ToJson(DataContainerConfig config) => throw new NotSupportedException();
		public void Save() => throw new NotSupportedException();
	}
}
