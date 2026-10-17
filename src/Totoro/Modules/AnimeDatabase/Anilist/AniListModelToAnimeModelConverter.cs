using System.Diagnostics;
using Flurl;
using Totoro.Core;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;
using Totoro.Modules.AnimeDatabase.Anilist.GraphQl;

namespace Totoro.Modules.AnimeDatabase.Anilist;

public static partial class AniListModelToAnimeModelConverter
{
    private static readonly Settings s_settings = Locator.GetRequiredService<IModuleSettings<Settings>>().Value;

    private static string GetTitle(Media media)
    {
        if (s_settings.TitleLanguage == TitleLanguage.English)
        {
            return media.Title.English ?? media.Title.Romaji;
        }

        return media.Title.Romaji ?? media.Title.English;
    }

    public static AnimeModel ConvertModel(Media media)
    {
        return new AnimeModel
        {
            Title = GetTitle(media),
            EngTitle = media.Title.English ?? media.Title.Romaji ?? string.Empty,
            RomajiTitle = media.Title.Romaji ?? media.Title.English ?? string.Empty,
            Id = media.Id ?? 0,
            ExternalIds = new AnimeId
            {
                Anilist = media.Id ?? 0,
                MyAnimeList = media.IdMal ?? 0
            },
            Image = media.CoverImage.Large,
            BannerImage = string.IsNullOrEmpty(media.BannerImage) ? media.CoverImage.Large : media.BannerImage,
            TotalEpisodes = media.Episodes,
            AiringStatus = ConvertStatus(media.Status),
            MeanScore = media.MeanScore,
            Popularity = media.Popularity ?? 0,
            Tracking = ConvertTracking(media.MediaListEntry),
            NextEpisodeAt = ConvertToExactTime(media.NextAiringEpisode?.TimeUntilAiring),
            AiredEpisodes = media.NextAiringEpisode?.Episode - 1 ?? 0,
            EndDate = ConvertDate(media.EndDate),
            Season = GetSeason(media.Season, media.SeasonYear),
            ServiceId = AnilistModule.Id,
            ServiceName = nameof(AnimeId.Anilist),
            // Requested as HTML (asHtml: true), so all of AniList's markup arrives as HTML; views render it with Totoro.UI.Text.HtmlText.
            Description = media.Description ?? string.Empty,
            Related = ConvertRelated(media.Relations?.Edges),
            Recommended = ConvertSimple(media.Recommendations?.Nodes.Select(x => x.MediaRecommendation).Where(x => x is not null)
                                             .Where(x => x.Type == MediaType.Anime) ?? []),
            Episodes = ConvertEpisodes(media),
            Url = $"https://anilist.co/anime/{media.Id}/",
            MediaFormat = ConvertMediaFormat(media.Format),
            Genres = [..media.Genres],
            Studios = [.. media.Studios?.Nodes.Where(x => x.IsAnimationStudio == true).Select(x => x.Name).Distinct() ?? []],
            Trailers = ConvertTrailers(media.Trailer),
            AlternateTitles = GetAlternateTitles(media)
        };
    }

    private static HashSet<string> GetAlternateTitles(Media? media)
    {
        var titles = new HashSet<string>();
        
        if(media?.Title is null)
        {
            return titles;
        }

        if (!string.IsNullOrEmpty(media.Title.English))
        {
            titles.Add(media.Title.English);
        }

        if (!string.IsNullOrEmpty(media.Title.Romaji))
        {
            titles.Add(media.Title.Romaji);
        }

        if (!string.IsNullOrEmpty(media.Title.Native))
        {
            titles.Add(media.Title.Native);
        }

        if (media.Synonyms is null)
        {
            return titles;
        }

        foreach (var synonym in media.Synonyms)
        {
            if (string.IsNullOrEmpty(synonym))
            {
                continue;
            }

            titles.Add(synonym);
        }

        return titles;
    }

    private static IReadOnlyCollection<TrailerVideo> ConvertTrailers(MediaTrailer? mediaTrailer)
    {
        if (mediaTrailer is null)
        {
            return [];
        }

        if (mediaTrailer.Site.Equals("youtube", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new TrailerVideo
                {
                    Url = "https://www.youtube.com/watch".AppendQueryParam("v", mediaTrailer.Id),
                    Title = "Trailer",
                    Thumbnail = mediaTrailer.Thumbnail
                }
            ];
        }

        return [];
    }

    private static AnimeMediaFormat ConvertMediaFormat(MediaFormat? mediaFormat)
    {
        return mediaFormat switch
        {
            MediaFormat.Tv or MediaFormat.TvShort => AnimeMediaFormat.Tv,
            MediaFormat.Movie => AnimeMediaFormat.Movie,
            MediaFormat.Special => AnimeMediaFormat.Special,
            MediaFormat.Ova => AnimeMediaFormat.Ova,
            MediaFormat.Ona => AnimeMediaFormat.Ona,
            MediaFormat.Music => AnimeMediaFormat.Music,
            _ => AnimeMediaFormat.Unknown
        };
    }

    private static List<EpisodeInfo> ConvertEpisodes(Media media)
    {
        if (media.StreamingEpisodes is null or { Count: 0 })
        {
            return [];
        }

        var result = new List<EpisodeInfo>();
        foreach (var episode in media.AiringSchedule.Nodes.Index())
        {
            var (index, node) = episode;
            if (node is null)
            {
                continue;
            }

            if (node.TimeUntilAiring is not null)
            {
                break;
            }

            var ep = new EpisodeInfo
            {
                EpisodeNumber = node.Episode ?? 0
            };

            if (media.StreamingEpisodes.Count > index)
            {
                var streamingEp = media.StreamingEpisodes.ElementAt(index);
                ep.Titles = new Titles
                {
                    English = streamingEp.Title
                };
                ep.Image = streamingEp.Thumbnail;
            }

            result.Add(ep);
        }

        return result;
    }

    /// <summary>Related anime, with how each relates (sequel, prequel...).</summary>
    public static AnimeModel[] ConvertRelated(IEnumerable<MediaEdge>? edges)
    {
        return
        [
            .. (edges ?? []).Where(x => x.Node is { Type: MediaType.Anime })
                            .Select(x =>
                            {
                                var anime = ConvertSimple([x.Node])[0];
                                anime.Relation = ConvertRelation(x.RelationType);
                                return anime;
                            })
        ];
    }

    public static AnimeRelationType? ConvertRelation(MediaRelation? relation) => relation switch
    {
        MediaRelation.Sequel => AnimeRelationType.Sequel,
        MediaRelation.Prequel => AnimeRelationType.Prequel,
        MediaRelation.SideStory => AnimeRelationType.SideStory,
        MediaRelation.Parent => AnimeRelationType.Parent,
        MediaRelation.Alternative => AnimeRelationType.Alternative,
        MediaRelation.Summary => AnimeRelationType.Summary,
        MediaRelation.SpinOff => AnimeRelationType.SpinOff,
        null => null,
        _ => AnimeRelationType.Other,
    };

    public static AnimeModel[] ConvertSimple(IEnumerable<Media>? media)
    {
        if (media is null)
        {
            return [];
        }

        return
        [
            .. media.Where(x => x.Type == MediaType.Anime)
                    .Select(x => new AnimeModel
                    {
                        Title = x.Title.Romaji ?? x.Title.English ?? string.Empty,
                        EngTitle = x.Title.English ?? x.Title.Romaji ?? string.Empty,
                        RomajiTitle = x.Title.Romaji ?? x.Title.English ?? string.Empty,
                        Description = x.Description,
                        Id = x.Id ?? 0,
                        ExternalIds = new AnimeId { Anilist = x.Id ?? 0, MyAnimeList = x.IdMal ?? 0 },
                        TotalEpisodes = x.Episodes,
                        Season = GetSeason(x.Season, x.SeasonYear),
                        Image = x.CoverImage.Large,
                        Tracking = ConvertTracking(x.MediaListEntry),
                        AiringStatus = ConvertStatus(x.Status),
                        ServiceId = AnilistModule.Id,
                        ServiceName = nameof(AnimeId.Anilist),
                        MediaFormat = ConvertMediaFormat(x.Format)
                    })
        ];
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

    public static DateTime? ConvertToExactTime(int? secondsTillAiring)
    {
        if (secondsTillAiring is null)
        {
            return null;
        }

        return DateTime.Now + TimeSpan.FromSeconds(secondsTillAiring.Value);
    }

    public static Tracking? ConvertTracking(MediaList? listEntry)
    {
        if (listEntry == null)
        {
            // uncomment for debugging other people list
            //return new Tracking
            //{
            //    Status = AnimeStatus.Dropped
            //};

            return null;
        }

        return new Tracking
        {
            WatchedEpisodes = listEntry.Progress is > 0 ? listEntry.Progress : null,
            Score = (int?)(listEntry.Score is > 0 ? listEntry.Score : null),
            Status = ConvertListStatus(listEntry.Status),
            StartDate = ConvertDate(listEntry.StartedAt),
            FinishDate = ConvertDate(listEntry.CompletedAt)
        };
    }

    public static ListItemStatus? ConvertListStatus(MediaListStatus? status)
    {
        return status switch
        {
            MediaListStatus.Current => ListItemStatus.Watching,
            MediaListStatus.Planning => ListItemStatus.PlanToWatch,
            MediaListStatus.Paused => ListItemStatus.OnHold,
            MediaListStatus.Dropped => ListItemStatus.Dropped,
            MediaListStatus.Completed => ListItemStatus.Completed,
            MediaListStatus.Repeating => ListItemStatus.Rewatching,
            _ => null
        };
    }

    public static MediaListStatus? ConvertListStatus(ListItemStatus? status)
    {
        return status switch
        {
            ListItemStatus.Watching => MediaListStatus.Current,
            ListItemStatus.PlanToWatch => MediaListStatus.Planning,
            ListItemStatus.OnHold => MediaListStatus.Paused,
            ListItemStatus.Completed => MediaListStatus.Completed,
            ListItemStatus.Dropped => MediaListStatus.Dropped,
            ListItemStatus.Rewatching => MediaListStatus.Repeating,
            _ => null
        };
    }


    public static DateTime? ConvertDate(FuzzyDate? date)
    {
        if (date?.Year is null || date.Month is null || date.Day is null)
        {
            return null;
        }

        return new DateTime(date.Year.Value, date.Month.Value, date.Day.Value);
    }

    public static FuzzyDateInput? ConvertDate(DateTime? date)
    {
        if (date is null)
        {
            return null;
        }

        return new FuzzyDateInput
        {
            Year = date.Value.Year,
            Month = date.Value.Month,
            Day = date.Value.Day
        };
    }

    public static MediaSeason? ConvertSeason(AnimeSeason? season)
    {
        return season switch
        {
            AnimeSeason.Spring => MediaSeason.Spring,
            AnimeSeason.Summer => MediaSeason.Summer,
            AnimeSeason.Fall => MediaSeason.Fall,
            AnimeSeason.Winter => MediaSeason.Winter,
            _ => null
        };
    }

    // AnimeSource mirrors MediaSource member for member.
    public static MediaSource? ConvertSource(AnimeSource? source)
    {
        return (MediaSource?)(int?)source;
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

    private static Season? GetSeason(MediaSeason? season, int? year)
    {
        if (season is null || year is null)
        {
            return null;
        }

        return new Season(ConvertSeason(season.Value), year.Value);
    }
}