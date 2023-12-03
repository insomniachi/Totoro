using Flurl;

namespace Totoro.Core.Services.Simkl
{
    internal class SimklToAnimeModelConverter
    {

        internal static AnimeModel Convert(SimklItemModel item)
        {
            return new AnimeModel
            {
                Title = item.Show.Title,
                Id = item.Show.Id.Simkl ?? item.Show.Id.Simkl2 ?? 0,
                MalId = GetMalId(item.Show.Id.MyAnimeList),
                TotalEpisodes = item.TotalEpisodesCount,
                Videos = ConvertTrailers(item.Show.Trailers),
                AlternativeTitles = new [] { item.Show.EnglishTitle },
                Season = ConvertSeason(item.Show.Season),
                MeanScore = item.Show.Ratings?.Simkl.Rating,
                Genres = item.Show.Genres ?? Enumerable.Empty<string>(),
                Related = ConvertSimple(item.Show.Relations),
                Image = $"https://wsrv.nl/?url=https://simkl.in/posters/{item.Show.Image}_m.jpg",
                AiringStatus = ConvertStatus(item),
                Tracking = ConvertTracking(item),
                Description = item.Show.Overview
            };
        }

        private static long? GetMalId(string myAnimeList)
        {
            return long.TryParse(myAnimeList, out var malId)
                ? malId
                : null;
        }

        internal static AnimeModel Convert(SimklMetaData item)
        {
            return new AnimeModel
            {
                Title = item.EnglishTitle ?? item.Title,
                Id = item.Id.Simkl ?? item.Id.Simkl2 ?? 0,
                MalId = GetMalId(item.Id.MyAnimeList),
                TotalEpisodes = item.TotalEpisodes,
                Videos = ConvertTrailers(item.Trailers),
                AlternativeTitles = new[] { item.EnglishTitle },
                Season = ConvertSeason(item.Season),
                MeanScore = item.Ratings?.Simkl.Rating,
                Genres = item.Genres ?? Enumerable.Empty<string>(),
                Related = ConvertSimple(item.Relations),
                Image = $"https://wsrv.nl/?url=https://simkl.in/posters/{item.Image}_m.jpg",
                Description = item.Overview
                //AiringStatus = ConvertStatus(item),
                //Tracking = ConvertTracking(item),
            };
        }

        private static Tracking ConvertTracking(SimklItemModel item)
        {
            return new Tracking
            {
                WatchedEpisodes = item.WatchedEpisodeCount,
                Status = item.Status switch
                {
                    "watching" => AnimeStatus.Watching,
                    "hold" => AnimeStatus.OnHold,
                    "plantowatch" => AnimeStatus.PlanToWatch,
                    "completed" => AnimeStatus.Completed,
                    "dropped" => AnimeStatus.Dropped,
                    _ => null
                }
            };
        }

        private static AiringStatus ConvertStatus(SimklItemModel item)
        {
            if(item.NotAiredEpisodesCount > 0)
            {
                return AiringStatus.CurrentlyAiring;
            }
            else if(item.NotAiredEpisodesCount == 0)
            {
                return AiringStatus.FinishedAiring;
            }

            return AiringStatus.NotYetAired;
        }

        private static AnimeModel[] ConvertSimple(List<SimklMetaData> relations)
        {
            if(relations is null)
            {
                return Array.Empty<AnimeModel>();
            }

            return relations.Select(item => new AnimeModel
            {
                Title = item.Title,
                Id = item.Id.Simkl ?? item.Id.Simkl2 ?? 0,
                Image = $"https://wsrv.nl/?url=https://simkl.in/posters/{item.Image}_m.jpg",
            }).ToArray();

        }

        private static List<Video> ConvertTrailers(List<Trailer> trailers)
        {
            if(trailers is null)
            {
                return new List<Video>();
            }

            return trailers.Select((x,i) => new Video
            {
                Url = $"https://www.youtube.com/watch?v={x.Youtube}",
                Title = $"Trailer {i+1}"
            }).ToList();
        }

        private static Season ConvertSeason(string season)
        {
            if(string.IsNullOrEmpty(season))
            {
                return null;
            }

            var parts = season.Split(' ');
            var seasonName = Enum.Parse<AnimeSeason>(parts[0].Trim());
            var year = int.Parse(parts[1].Trim());
            return new Season(seasonName, year);
        }
    }
}
