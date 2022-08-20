using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using AnimDL.UI.Core.Contracts;
using AnimDL.UI.Core.Models;
using MalApi.Interfaces;

namespace AnimDL.UI.Core.Services.MyAnimeList;

public class MyAnimeListTrackingService : ITrackingService
{
    private readonly IMalClient _client;
    private readonly MalToModelConverter _converter;

    public MyAnimeListTrackingService(IMalClient client,
                                      MalToModelConverter converter)
    {
        _client = client;
        _converter = converter;
    }

    public IObservable<IEnumerable<AnimeModel>> GetAnime()
    {
        return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
        {
            try
            {
                var watching = await _client.Anime().OfUser().WithStatus(MalApi.AnimeStatus.Watching)
                                     .WithField(x => x.UserStatus).WithField(x => x.TotalEpisodes)
                                     .WithField(x => x.Broadcast).WithField(x => x.MeanScore)
                                     .Find();

                observer.OnNext(ConvertToAnimeModel(watching.Data));

                var all = await _client.Anime().OfUser()
                                       .WithField(x => x.UserStatus).WithField(x => x.TotalEpisodes)
                                       .WithField(x => x.Broadcast).WithField(x => x.MeanScore)
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
                var pagedAnime = await _client.Anime().OfUser().WithStatus(MalApi.AnimeStatus.Watching)
                                 .WithField(x => x.UserStatus).WithField(x => x.TotalEpisodes)
                                 .WithField(x => x.Broadcast).WithField(x => x.MeanScore)
                                 .WithField(x => x.Status)
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

        return request.Publish().ToObservable().Select(x => new Tracking
        {
            WatchedEpisodes = x.WatchedEpisodes,
            Status = (AnimeStatus)(int)x.Status,
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
