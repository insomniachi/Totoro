using MalApi.Interfaces;

namespace Totoro.Core.Services.MyAnimeList;

public class MyAnimeListTrackingService : ITrackingService
{
    private readonly IMalClient _client;
    private readonly MalToModelConverter _converter;
    private static readonly string[] FieldNames = new[]
    {
        MalApi.AnimeFieldNames.UserStatus,
        MalApi.AnimeFieldNames.TotalEpisodes,
        MalApi.AnimeFieldNames.Broadcast,
        MalApi.AnimeFieldNames.Mean,
        MalApi.AnimeFieldNames.Status,
        MalApi.AnimeFieldNames.AlternativeTitles
    };

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
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            try
            {
                var watching = await _client.Anime().OfUser().WithStatus(MalApi.AnimeStatus.Watching).IncludeNsfw()
                                     .WithField(x => x.UserStatus).WithField(x => x.TotalEpisodes)
                                     .WithField(x => x.Broadcast).WithField(x => x.MeanScore)
                                     .WithField(x => x.Status).WithField(x => x.AlternativeTitles)
                                     .Find();

                observer.OnNext(ConvertToAnimeModel(watching.Data));

                var all = await _client.Anime().OfUser().IncludeNsfw()
                                       .WithField(x => x.UserStatus).WithField(x => x.TotalEpisodes)
                                       .WithField(x => x.Broadcast).WithField(x => x.MeanScore)
                                       .WithField(x => x.Status).WithField(x => x.AlternativeTitles)
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

    public IObservable<IEnumerable<ScheduledAnimeModel>> GetCurrentlyAiringTrackedAnime()
    {
        return Observable.Create<IEnumerable<ScheduledAnimeModel>>(async observer =>
        {
            try
            {
                var pagedAnime = await _client.Anime().OfUser().WithStatus(MalApi.AnimeStatus.Watching).IncludeNsfw()
                                 .WithField(x => x.UserStatus).WithField(x => x.TotalEpisodes)
                                 .WithField(x => x.Broadcast).WithField(x => x.MeanScore)
                                 .WithField(x => x.Status).WithField(x => x.AlternativeTitles)
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

    public IObservable<Tracking> Update(long id, Tracking tracking)
    {
        var request = _client.Anime().WithId(id).UpdateStatus();

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

        return request.Publish().ToObservable().Select(x => new Tracking
        {
            WatchedEpisodes = x.WatchedEpisodes,
            Status = (AnimeStatus)(int)x.Status,
            Score = (int)x.Score,
            UpdatedAt = x.UpdatedAt
        });
        ;
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
