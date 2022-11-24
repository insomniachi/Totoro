using MalApi.Interfaces;
using Splat;

namespace Totoro.Core.Services.MyAnimeList;

public class MyAnimeListTrackingService : ITrackingService, IEnableLogger
{
    private readonly IMalClient _client;
    private readonly MalToModelConverter _converter;
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
        MalApi.AnimeFieldNames.Status
    };

    public bool IsAuthenticated => _client.IsAuthenticated;

    public MyAnimeListTrackingService(IMalClient client,
                                      MalToModelConverter converter)
    {
        _client = client;
        _converter = converter;
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
            .Select(paged => paged.Data)
            .Select(ConvertToAnimeModel);
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime()
    {
        if(IsAuthenticated)
        {
            return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
            {
                try
                {
                    var watching = await _client.Anime()
                                                .OfUser()
                                                .WithStatus(MalApi.AnimeStatus.Watching)
                                                .IncludeNsfw()
                                                .WithFields(FieldNames)
                                                .Find();

                    observer.OnNext(ConvertToAnimeModel(watching.Data));

                    var all = await _client.Anime()
                                           .OfUser()
                                           .IncludeNsfw()
                                           .WithFields(FieldNames)
                                           .Find();

                    observer.OnNext(ConvertToAnimeModel(all.Data));
                    observer.OnCompleted();
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            });
        }
        else
        {
            return Observable.Empty<IEnumerable<AnimeModel>>();
        }
    }

    public IObservable<IEnumerable<ScheduledAnimeModel>> GetCurrentlyAiringTrackedAnime()
    {
        if(IsAuthenticated)
        {
            return Observable.Create<IEnumerable<ScheduledAnimeModel>>(async observer =>
            {
                try
                {
                    var pagedAnime = await _client.Anime()
                                                  .OfUser()
                                                  .WithStatus(MalApi.AnimeStatus.Watching)
                                                  .IncludeNsfw()
                                                  .WithFields(FieldNames)
                                                  .Find();

                    observer.OnNext(ConvertToScheduledAnimeModel(pagedAnime.Data.Where(x => x.Status == MalApi.AiringStatus.CurrentlyAiring).ToList()));

                    while (!string.IsNullOrEmpty(pagedAnime.Paging.Next))
                    {
                        pagedAnime = await _client.GetNextAnimePage(pagedAnime);
                        observer.OnNext(ConvertToScheduledAnimeModel(pagedAnime.Data.Where(x => x.Status == MalApi.AiringStatus.CurrentlyAiring).ToList()));
                    }

                    observer.OnCompleted();
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            });
        }
        else
        {
            return Observable.Empty<IEnumerable<ScheduledAnimeModel>>();
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
            .Do(_ => this.Log().Debug("Tracking Updated."))
            .Select(x => new Tracking
            {
                WatchedEpisodes = x.WatchedEpisodes,
                Status = (AnimeStatus)(int)x.Status,
                Score = (int)x.Score,
                UpdatedAt = x.UpdatedAt
            });
    }

    private IEnumerable<AnimeModel> ConvertToAnimeModel(List<MalApi.Anime> anime)
    {
        return anime.Select(x => _converter.Convert<AnimeModel>(x));
    }

    private IEnumerable<ScheduledAnimeModel> ConvertToScheduledAnimeModel(List<MalApi.Anime> anime)
    {
        return anime.Select(x => _converter.Convert<ScheduledAnimeModel>(x) as ScheduledAnimeModel);
    }
}
