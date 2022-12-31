using System.Globalization;
using MalApi.Interfaces;
using Splat;
using static Totoro.Core.Models.MalToModelConverter;

namespace Totoro.Core.Services.MyAnimeList;

public class MyAnimeListTrackingService : ITrackingService, IEnableLogger
{
    private readonly IMalClient _client;
    private static readonly string[] FieldNames = new[]
    {
        MalApi.AnimeFieldNames.Synopsis,
        MalApi.AnimeFieldNames.TotalEpisodes,
        MalApi.AnimeFieldNames.Broadcast,
        MalApi.AnimeFieldNames.UserStatus,
        MalApi.AnimeFieldNames.NumberOfUsers,
        MalApi.AnimeFieldNames.Rank,
        MalApi.AnimeFieldNames.Mean,
        MalApi.AnimeFieldNames.AlternativeTitles,
        MalApi.AnimeFieldNames.Popularity,
        MalApi.AnimeFieldNames.StartSeason,
        MalApi.AnimeFieldNames.Genres,
        MalApi.AnimeFieldNames.Status,
        MalApi.AnimeFieldNames.Videos,
        MalApi.AnimeFieldNames.EndDate,
        MalApi.AnimeFieldNames.StartDate,
    };

    public bool IsAuthenticated => _client.IsAuthenticated;
    public ListServiceType Type => ListServiceType.MyAnimeList;

    public MyAnimeListTrackingService(IMalClient client)
    {
        _client = client;
    }

    public IObservable<IEnumerable<AnimeModel>> GetWatchingAnime()
    {
        return _client
            .Anime()
            .OfUser()
            .WithStatus(MalApi.AnimeStatus.Watching)
            .IncludeNsfw()
            .WithFields(FieldNames)
            .Find().ToObservable()
            .Select(paged => paged.Data.Select(ConvertModel));
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime()
    {
        if(IsAuthenticated)
        {
            return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
            {
                var watching = await _client.Anime()
                                            .OfUser()
                                            .WithStatus(MalApi.AnimeStatus.Watching)
                                            .IncludeNsfw()
                                            .WithFields(FieldNames)
                                            .Find();

                observer.OnNext(watching.Data.Select(ConvertModel));

                var all = await _client.Anime()
                                       .OfUser()
                                       .IncludeNsfw()
                                       .WithFields(FieldNames)
                                       .Find();

                observer.OnNext(all.Data.Select(ConvertModel));
                observer.OnCompleted();
                return Disposable.Empty;
            });
        }
        else
        {
            return Observable.Empty<IEnumerable<AnimeModel>>();
        }
    }

    public IObservable<IEnumerable<AnimeModel>> GetCurrentlyAiringTrackedAnime()
    {
        if(IsAuthenticated)
        {
            return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
            {
                var pagedAnime = await _client.Anime()
                                              .OfUser()
                                              .WithStatus(MalApi.AnimeStatus.Watching)
                                              .IncludeNsfw()
                                              .WithFields(FieldNames)
                                              .Find();

                observer.OnNext(pagedAnime.Data.Where(CurrentlyAiringOrFinishedToday).Select(ConvertModel));

                while (!string.IsNullOrEmpty(pagedAnime.Paging.Next))
                {
                    pagedAnime = await _client.GetNextAnimePage(pagedAnime);
                    observer.OnNext(pagedAnime.Data.Where(CurrentlyAiringOrFinishedToday).Select(ConvertModel));
                }

                observer.OnCompleted();

                return Disposable.Empty;
            });
        }
        else
        {
            return Observable.Empty<IEnumerable<AnimeModel>>();
        }
    }

    public IObservable<Tracking> Update(long id, Tracking tracking)
    {
        if(!IsAuthenticated)
        {
            return Observable.Return(tracking);
        }

        var request = _client.Anime().WithId(id).UpdateStatus().WithTags("Totoro");

        if (tracking.WatchedEpisodes is { } ep)
        {
            request.WithEpisodesWatched(ep);
        }

        if (tracking.Status is { } status)
        {
            request.WithStatus((MalApi.AnimeStatus)(int)status);
        }

        if (tracking.Score is { } score)
        {
            request.WithScore((MalApi.Score)score);
        }

        if(tracking.StartDate is { } sd)
        {
            request.WithStartDate(sd);
        }

        if(tracking.FinishDate is { } fd)
        {
            request.WithFinishDate(fd);
        }

        return request
            .Publish()
            .ToObservable()
            .Select(x => new Tracking
            {
                WatchedEpisodes = x.WatchedEpisodes,
                Status = (AnimeStatus)(int)x.Status,
                Score = (int)x.Score,
                UpdatedAt = x.UpdatedAt
            })
            .Do(tracking => this.Log().Debug("Tracking Updated {0}.", tracking));
    }

    private static bool CurrentlyAiringOrFinishedToday(MalApi.Anime anime)
    {
        if(anime.Status == MalApi.AiringStatus.CurrentlyAiring)
        {
            return true;
        }

        if(!DateTime.TryParseExact(anime.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,  out DateTime date))
        {
            return false;
        }

        return DateTime.Today == date;
    }
}
