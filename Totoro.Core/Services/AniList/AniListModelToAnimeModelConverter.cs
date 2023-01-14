using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Totoro.Core.Services.AniList
{
    public partial class AniListModelToAnimeModelConverter
    {
        [GeneratedRegex(@"(</?i>)|(<br>)")]
        private static partial Regex DescriptionCleanRegex();

        public static AnimeModel ConvertModel(Media media)
        {
            return new AnimeModel
            {
                Title = media.Title.Romaji ?? media.Title.English ?? string.Empty,
                Id = media.Id ?? 0,
                Image = media.CoverImage.Large,
                TotalEpisodes = media.Episodes,
                AiringStatus = ConvertStatus(media.Status),
                MeanScore = media.MeanScore,
                Popularity = media.Popularity ?? 0,
                Description = DescriptionCleanRegex().Replace(media.Description ?? string.Empty, string.Empty),
                Videos = ConvertTrailer(media.Trailer),
                Tracking = ConvertTracking(media.MediaListEntry),
                BroadcastDay = GetBroadcastDay(media.StartDate),
                Season = GetSeason(media.Season, media.SeasonYear),
                AlternativeTitles = GetAlternateTiltes(media.Title),
                AiredEpisodes = media.NextAiringEpisode?.Episode - 1 ?? 0
            };
        }

        public static AiringStatus ConvertStatus(MediaStatus? status)
        {
            return status switch
            {
                MediaStatus.Releasing => AiringStatus.CurrentlyAiring,
                MediaStatus.Finished => AiringStatus.FinishedAiring,
                _ => AiringStatus.NotYetAired
            };
        }

        public static Tracking ConvertTracking(MediaList listEntry)
        {
            if (listEntry == null)
            {
                return null;
            }

            return new Tracking
            {
                WatchedEpisodes = listEntry.Progress,
                Score = (int)listEntry.Score,
                Status = ConvertListStatus(listEntry.Status),
                StartDate = ConvertDate(listEntry.StartedAt),
                FinishDate = ConvertDate(listEntry.CompletedAt),
            };
        }

        public static AnimeStatus? ConvertListStatus(MediaListStatus? status)
        {
            return status switch
            {
                MediaListStatus.Current => AnimeStatus.Watching,
                MediaListStatus.Planning => AnimeStatus.PlanToWatch,
                MediaListStatus.Paused => AnimeStatus.OnHold,
                MediaListStatus.Dropped => AnimeStatus.Dropped,
                MediaListStatus.Completed => AnimeStatus.Completed,
                _ => null
            };
        }

        public static MediaListStatus? ConvertListStatus(AnimeStatus? status)
        {
            return status switch
            {
                AnimeStatus.Watching => MediaListStatus.Current,
                AnimeStatus.PlanToWatch => MediaListStatus.Planning,
                AnimeStatus.OnHold => MediaListStatus.Paused,
                AnimeStatus.Completed => MediaListStatus.Completed,
                AnimeStatus.Dropped => MediaListStatus.Dropped,
                _ => null
            };
        }

        public static MediaSeason ConvertSeason(AnimeSeason season)
        {
            return season switch
            {
                AnimeSeason.Spring => MediaSeason.Spring,
                AnimeSeason.Summer => MediaSeason.Summer,
                AnimeSeason.Fall => MediaSeason.Fall,
                AnimeSeason.Winter => MediaSeason.Winter,
                _ => throw new UnreachableException()
            };
        }

        public static AnimeSeason ConvertSeason(MediaSeason season)
        {
            return season switch
            {
                MediaSeason.Spring => AnimeSeason.Spring,
                MediaSeason.Summer => AnimeSeason.Summer,
                MediaSeason.Fall => AnimeSeason.Fall,
                MediaSeason.Winter => AnimeSeason.Winter,
                _ => throw new UnreachableException()
            };
        }


        public static DateTime? ConvertDate(FuzzyDate date)
        {
            if (date.Year is null || date.Month is null || date.Day is null)
            {
                return null;
            }

            return new DateTime(date.Year.Value, date.Month.Value, date.Day.Value);
        }

        public static FuzzyDateInput ConvertDate(DateTime? date)
        {
            if (date is null)
            {
                return null;
            }

            return new FuzzyDateInput
            {
                Year = date.Value.Year,
                Month = date.Value.Month,
                Day = date.Value.Day,
            };
        }

        private static DayOfWeek? GetBroadcastDay(FuzzyDate date)
        {
            if (date.Year is null || date.Month is null || date.Day is null)
            {
                return null;
            }

            return new DateOnly(date.Year.Value, date.Month.Value, date.Day.Value).DayOfWeek;
        }

        private static Season GetSeason(MediaSeason? season, int? year)
        {
            if (season is null || year is null)
            {
                return null;
            }

            return new Season(ConvertSeason(season.Value), year.Value);
        }

        private static List<string> GetAlternateTiltes(MediaTitle title)
        {
            var list = new List<string>();

            if (!string.IsNullOrEmpty(title.English))
            {
                list.Add(title.English);
            }
            if (!string.IsNullOrEmpty(title.Romaji))
            {
                list.Add(title.Romaji);
            }
            if (!string.IsNullOrEmpty(title.Native))
            {
                list.Add(title.Native);
            }

            return list;
        }

        private static List<Video> ConvertTrailer(MediaTrailer trailer)
        {
            if(trailer is null)
            {
                return new List<Video>();
            }

            return new List<Video>
            {
                new Video
                {
                    Title = "Trailer",
                    Url = $"https://www.youtube.com/watch?v={trailer.Id}/",
                    Thumbnail = trailer.Thumbnail
                }
            };
        }
    }
}
