using System.Globalization;
using System.Net;
using MalApi;
using Totoro.Core.Modules.Anime;
using AiringStatus = Totoro.Core.Modules.Anime.AiringStatus;
using AnimeSeason = Totoro.Core.Modules.Anime.AnimeSeason;
using Season = Totoro.Core.Modules.Anime.Season;

namespace Totoro.Modules.AnimeDatabase.MyAnimeList;

internal static class MalToModelConverter
{
	/// <summary>The fields requested for anime in lists (the user's list, search results...).</summary>
	public static readonly string[] ListFields =
	[
		AnimeFieldNames.Synopsis,
		AnimeFieldNames.TotalEpisodes,
		AnimeFieldNames.Broadcast,
		AnimeFieldNames.UserStatus,
		AnimeFieldNames.NumberOfUsers,
		AnimeFieldNames.Rank,
		AnimeFieldNames.Mean,
		AnimeFieldNames.AlternativeTitles,
		AnimeFieldNames.Popularity,
		AnimeFieldNames.StartSeason,
		AnimeFieldNames.Genres,
		AnimeFieldNames.Status,
		AnimeFieldNames.Videos,
		AnimeFieldNames.StartDate,
		AnimeFieldNames.EndDate,
		AnimeFieldNames.MediaType
	];

	private static readonly TimeZoneInfo s_japan = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

	public static AnimeModel ConvertModel(MalApi.Anime malModel)
	{
		var engTitle = malModel.AlternativeTitles?.English;

		var model = new AnimeModel
		{
			Id = malModel.Id,
			ExternalIds = new AnimeId { MyAnimeList = malModel.Id },
			Title = malModel.Title,
			EngTitle = string.IsNullOrEmpty(engTitle) ? malModel.Title : engTitle,
			RomajiTitle = malModel.Title,
			Image = malModel.MainPicture?.Large ?? "",
			// MAL has no banners; the poster is the best there is.
			BannerImage = malModel.MainPicture?.Large ?? "",
			ServiceId = MyAnimeListModule.Id,
			ServiceName = nameof(AnimeId.MyAnimeList),
			Description = ToHtml(malModel.Synopsis),
			Url = $"https://myanimelist.net/anime/{malModel.Id}/",
			MediaFormat = ConvertFormat(malModel.MediaType),
			Genres = malModel.Genres is { } genres ? [.. genres.Select(x => x.Name)] : [],
			Studios = malModel.Studios is { } studios ? [.. studios.Select(x => x.Name)] : [],
			Trailers = malModel.Videos is { } videos ? [.. videos.Select(x => new TrailerVideo { Thumbnail = x.Thumbnail, Title = x.Title, Url = x.Url })] : [],
			AlternateTitles = GetAlternateTitles(malModel),
			Tracking = ConvertTracking(malModel.UserStatus),
			AiringStatus = (AiringStatus)(int)(malModel.Status ?? MalApi.AiringStatus.NotYetAired),
			TotalEpisodes = malModel.TotalEpisodes is > 0 ? malModel.TotalEpisodes : null,
			// Out of 100, like AniList's (see AnimeModel.MeanScore); MyAnimeList's is out of 10.
			MeanScore = malModel.MeanScore * 10,
			Popularity = malModel.Popularity ?? 0,
		};

		if (malModel.StartSeason is { } season && ConvertSeason(season.SeasonName) is { } seasonName)
		{
			model.Season = new Season(seasonName, season.Year);
		}

		if (malModel is { Broadcast: { DayOfWeek: { } day } broadcast, Status: MalApi.AiringStatus.CurrentlyAiring } &&
			TimeSpan.TryParseExact(broadcast.StartTime, @"hh\:mm", CultureInfo.InvariantCulture, out var time))
		{
			model.NextEpisodeAt = NextBroadcast(day, time);
			if (DateTime.TryParseExact(malModel.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
			{
				model.AiredEpisodes = CountAiredEpisodes(start + time, TimeZoneInfo.ConvertTime(DateTime.Now, s_japan));
			}
		}

		if (malModel.RelatedAnime is { Length: > 0 } related)
		{
			model.Related =
			[
				.. related.Select(x =>
				{
					var anime = ConvertModel(x.Anime);
					anime.Relation = x.RelationType switch
					{
						MalApi.RelationType.Sequel => AnimeRelationType.Sequel,
						MalApi.RelationType.Prequel => AnimeRelationType.Prequel,
						MalApi.RelationType.SideStory => AnimeRelationType.SideStory,
						MalApi.RelationType.ParentStory or MalApi.RelationType.FullStory => AnimeRelationType.Parent,
						MalApi.RelationType.AlternativeSetting or MalApi.RelationType.AlternativeVersion => AnimeRelationType.Alternative,
						MalApi.RelationType.Summary => AnimeRelationType.Summary,
						_ => AnimeRelationType.Other,
					};
					return anime;
				}),
			];
		}

		if (malModel.Recommendations is { Length: > 0 } recommended)
		{
			model.Recommended = [.. recommended.Select(x => ConvertModel(x.Anime))];
		}

		return model;
	}

	public static AnimeModel ConvertJikanModel(JikanDotNet.Anime jikanModel)
	{
		var id = jikanModel.MalId ?? 0;
		var title = GetTitle("Default");
		var engTitle = GetTitle("English");

		return new AnimeModel
		{
			Id = id,
			ExternalIds = new AnimeId { MyAnimeList = id },
			Title = title,
			EngTitle = string.IsNullOrEmpty(engTitle) ? title : engTitle,
			RomajiTitle = title,
			Image = jikanModel.Images?.JPG?.LargeImageUrl ?? jikanModel.Images?.JPG?.ImageUrl ?? "",
			BannerImage = jikanModel.Images?.JPG?.LargeImageUrl ?? "",
			ServiceId = MyAnimeListModule.Id,
			ServiceName = nameof(AnimeId.MyAnimeList),
			Description = ToHtml(jikanModel.Synopsis),
			Url = $"https://myanimelist.net/anime/{id}/",
			MeanScore = (float?)jikanModel.Score * 10,
			TotalEpisodes = jikanModel.Episodes is > 0 ? jikanModel.Episodes : null,
			AiringStatus = jikanModel.Status switch
			{
				"Currently Airing" => AiringStatus.CurrentlyAiring,
				"Not yet aired" => AiringStatus.NotYetAired,
				_ => AiringStatus.FinishedAiring
			},
			Genres = jikanModel.Genres is { } genres ? [.. genres.Select(x => x.Name)] : [],
			AlternateTitles = jikanModel.Titles is { } titles ? titles.Select(x => x.Title).ToHashSet() : [],
			MediaFormat = jikanModel.Type switch
			{
				"TV" => AnimeMediaFormat.Tv,
				"OVA" => AnimeMediaFormat.Ova,
				"Movie" => AnimeMediaFormat.Movie,
				"Special" => AnimeMediaFormat.Special,
				"ONA" => AnimeMediaFormat.Ona,
				"Music" => AnimeMediaFormat.Music,
				_ => AnimeMediaFormat.Unknown
			}
		};

		string GetTitle(string type)
		{
			return jikanModel.Titles?.FirstOrDefault(x => x.Type == type)?.Title ?? "";
		}
	}

	public static Tracking? ConvertTracking(UserAnimeStatus? status)
	{
		if (status is null)
		{
			return null;
		}

		return new Tracking
		{
			WatchedEpisodes = status.WatchedEpisodes,
			Status = status.IsRewatching
				? ListItemStatus.Rewatching
				: status.Status switch
				{
					AnimeStatus.Watching => ListItemStatus.Watching,
					AnimeStatus.Completed => ListItemStatus.Completed,
					AnimeStatus.OnHold => ListItemStatus.OnHold,
					AnimeStatus.PlanToWatch => ListItemStatus.PlanToWatch,
					AnimeStatus.Dropped => ListItemStatus.Dropped,
					_ => null
				},
			Score = (int)status.Score,
			UpdatedAt = NullIfDefault(status.UpdatedAt),
			StartDate = NullIfDefault(status.StartDate),
			FinishDate = NullIfDefault(status.FinishDate)
		};
	}

	/// <summary>Our status as MAL's. Rewatching isn't a status on MAL (see MyAnimeListTracker.Update).</summary>
	public static AnimeStatus ConvertStatus(ListItemStatus status)
	{
		return status switch
		{
			ListItemStatus.Watching => AnimeStatus.Watching,
			ListItemStatus.Completed => AnimeStatus.Completed,
			ListItemStatus.OnHold => AnimeStatus.OnHold,
			ListItemStatus.PlanToWatch => AnimeStatus.PlanToWatch,
			ListItemStatus.Dropped => AnimeStatus.Dropped,
			_ => AnimeStatus.Watching
		};
	}

	/// <summary>
	/// MAL synopses are plain text, while the app renders descriptions as HTML (see AnimeModel.Description); keep the
	/// paragraphs, which HTML would otherwise run together.
	/// </summary>
	private static string ToHtml(string? text)
	{
		return string.IsNullOrEmpty(text) ? "" : WebUtility.HtmlEncode(text.Trim()).ReplaceLineEndings("<br>");
	}

	private static DateTime? NullIfDefault(DateTime value) => value == default ? null : value;

	private static HashSet<string> GetAlternateTitles(MalApi.Anime malModel)
	{
		if (malModel.AlternativeTitles is not { } alt)
		{
			return [];
		}

		HashSet<string> titles = [.. alt.Aliases ?? [], alt.English, alt.Japanese];
		titles.RemoveWhere(string.IsNullOrEmpty);
		return titles;
	}

	private static AnimeMediaFormat ConvertFormat(AnimeMediaType? mediaType)
	{
		return mediaType switch
		{
			AnimeMediaType.TV => AnimeMediaFormat.Tv,
			AnimeMediaType.OVA => AnimeMediaFormat.Ova,
			AnimeMediaType.Movie => AnimeMediaFormat.Movie,
			AnimeMediaType.Special => AnimeMediaFormat.Special,
			AnimeMediaType.ONA => AnimeMediaFormat.Ona,
			AnimeMediaType.Music => AnimeMediaFormat.Music,
			_ => AnimeMediaFormat.Unknown
		};
	}

	private static AnimeSeason? ConvertSeason(MalApi.AnimeSeason season)
	{
		// Not a numeric cast: MAL orders its seasons Winter, Summer, Spring, Fall.
		return season switch
		{
			MalApi.AnimeSeason.Winter => AnimeSeason.Winter,
			MalApi.AnimeSeason.Spring => AnimeSeason.Spring,
			MalApi.AnimeSeason.Summer => AnimeSeason.Summer,
			MalApi.AnimeSeason.Fall => AnimeSeason.Fall,
			_ => null
		};
	}

	/// <summary>The next broadcast, in local time, of a show that airs weekly at the given Japanese day and time.</summary>
	private static DateTime NextBroadcast(DayOfWeek day, TimeSpan time)
	{
		var japanNow = TimeZoneInfo.ConvertTime(DateTime.Now, s_japan);
		var days = ((int)day - (int)japanNow.DayOfWeek + 7) % 7;
		if (days == 0 && japanNow.TimeOfDay > time)
		{
			days = 7;
		}

		return TimeZoneInfo.ConvertTime(japanNow.Date.AddDays(days) + time, s_japan, TimeZoneInfo.Local);
	}

	/// <summary>How many weekly episodes have aired since the first one (both times in Japanese time).</summary>
	private static int CountAiredEpisodes(DateTime firstAired, DateTime now)
	{
		return now < firstAired ? 0 : (int)((now - firstAired).TotalDays / 7) + 1;
	}
}
