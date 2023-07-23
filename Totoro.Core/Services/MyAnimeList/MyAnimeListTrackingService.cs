using System.Globalization;
using MalApi.Interfaces;
using Microsoft.Extensions.Configuration;
using Splat;
using static Totoro.Core.Services.MyAnimeList.MalToModelConverter;

namespace Totoro.Core.Services.MyAnimeList;

public class MyAnimeListTrackingService : ITrackingService, IEnableLogger
{
    private readonly IMalClient _client;
    private readonly IAnilistService _anilistService;
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
        MalApi.AnimeFieldNames.MediaType
    };

    public bool IsAuthenticated => _client.IsAuthenticated;
    public ListServiceType Type => ListServiceType.MyAnimeList;

    public MyAnimeListTrackingService(IMalClient client,
                                      IConfiguration configuration,
                                      ILocalSettingsService localSettingsService,
                                      IAnilistService animeScheduleService)
    {
        _client = client;
        _anilistService = animeScheduleService;

        var token = localSettingsService.ReadSetting<MalApi.OAuthToken>("MalToken");
        var clientId = configuration["ClientId"];
        if ((DateTime.UtcNow - (token?.CreateAt ?? DateTime.UtcNow)).Days >= 28)
        {
            token = MalApi.MalAuthHelper.RefreshToken(clientId, token.RefreshToken).Result;
            localSettingsService.SaveSetting("MalToken", token);
        }
        if (token is not null && !string.IsNullOrEmpty(token.AccessToken))
        {
            client.SetAccessToken(token.AccessToken);
        }
        client.SetClientId(clientId);
    }

    public void SetAccessToken(string accessToken) => _client.SetAccessToken(accessToken);

    public IObservable<IEnumerable<AnimeModel>> GetAnime()
    {
        if (IsAuthenticated)
        {
            return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
            {
                var watching = await _client.Anime()
                                            .OfUser()
                                            .WithStatus(MalApi.AnimeStatus.Watching)
                                            .IncludeNsfw()
                                            .WithFields(FieldNames)
                                            .Find();

                var list = new List<AnimeModel>();

                foreach (var item in watching.Data)
                {
                    var model = ConvertModel(item);

                    GetAiredEpisodes(model)
                    .ToObservable()
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(x => model.AiredEpisodes = x);

                    list.Add(model);
                }

                observer.OnNext(list);

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
        if (IsAuthenticated)
        {
            return Observable.Create<IEnumerable<AnimeModel>>(async observer =>
            {
                var pagedAnime = await _client.Anime()
                                              .OfUser()
                                              .WithStatus(MalApi.AnimeStatus.Watching)
                                              .IncludeNsfw()
                                              .WithFields(FieldNames)
                                              .Find();

                var data = pagedAnime.Data.Where(CurrentlyAiringOrFinishedToday).Select(ConvertModel).ToList();
                observer.OnNext(data);

                foreach (var item in data)
                {

                    _anilistService
                        .GetNextAiringEpisodeTime(item.Id)
                        .ToObservable()
                        .ObserveOn(RxApp.MainThreadScheduler)
                        .Subscribe(x => item.NextEpisodeAt = x);
                }

                while (!string.IsNullOrEmpty(pagedAnime.Paging.Next))
                {
                    pagedAnime = await _client.GetNextAnimePage(pagedAnime);
                    data = pagedAnime.Data.Where(CurrentlyAiringOrFinishedToday).Select(ConvertModel).ToList();
                    observer.OnNext(data);

                    foreach (var item in data)
                    {
                        await Task.Delay(100);

                        _anilistService
                            .GetNextAiringEpisodeTime(item.Id)
                            .ToObservable()
                            .ObserveOn(RxApp.MainThreadScheduler)
                            .Subscribe(x => item.NextEpisodeAt = x);
                    }
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
        if (!IsAuthenticated)
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

        if (tracking.StartDate is { } sd)
        {
            request.WithStartDate(sd);
        }

        if (tracking.FinishDate is { } fd)
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
        if (anime.Status == MalApi.AiringStatus.CurrentlyAiring)
        {
            return true;
        }

        if (!DateTime.TryParseExact(anime.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            return false;
        }

        return DateTime.Today == date;
    }

    private async Task<int> GetAiredEpisodes(AnimeModel model)
    {
        var nextEp = await _anilistService.GetNextAiringEpisode(model.Id);
        return nextEp - 1 ?? 0;
    }
}
