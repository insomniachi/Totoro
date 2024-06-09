using System.Diagnostics;
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
    private static readonly string[] _fieldNames =
    [
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
    ];

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

    public async IAsyncEnumerable<AnimeModel> GetAnime()
    {
        if (!IsAuthenticated)
        {
            yield break;
        }

        var result = await _client.Anime()
                        .OfUser()
                        .IncludeNsfw()
                        .WithFields(_fieldNames)
                        .Find();

        foreach (var item in result.Data)
        {
            var model = ConvertModel(item);

            if (model.Tracking.Status == AnimeStatus.Watching && model.AiringStatus == AiringStatus.CurrentlyAiring)
            {
                Observable
                    .FromAsync(_ => _anilistService.GetNextAiringEpisode(item.Id), RxApp.TaskpoolScheduler)
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Subscribe(x =>
                    {
                        model.AiredEpisodes = x.Episode - 1 ?? 0;
                        model.NextEpisodeAt = x.Time;
                    });
            }

            yield return model;
        }
    }

    public async IAsyncEnumerable<AnimeModel> GetCurrentlyAiringTrackedAnime()
    {
        if (!IsAuthenticated)
        {
            yield break;
        }

        var pagedAnime = await _client.Anime()
                                        .OfUser()
                                        .WithStatus(MalApi.AnimeStatus.Watching)
                                        .IncludeNsfw()
                                        .WithFields(_fieldNames)
                                        .Find();

        foreach (var item in pagedAnime.Data.Where(CurrentlyAiringOrFinishedToday).Select(ConvertModel))
        {
            yield return item;
        }
        var data = pagedAnime.Data.Where(CurrentlyAiringOrFinishedToday).Select(ConvertModel).ToList();

        while (!string.IsNullOrEmpty(pagedAnime.Paging.Next))
        {
            pagedAnime = await _client.GetNextAnimePage(pagedAnime);
            foreach (var item in pagedAnime.Data.Where(CurrentlyAiringOrFinishedToday).Select(ConvertModel))
            {
                yield return item;
            }
        }
    }

    public async Task<Tracking> Update(long id, Tracking tracking)
    {
        if (!IsAuthenticated)
        {
            return tracking;
        }

        var request = _client.Anime().WithId(id).UpdateStatus().WithTags("Totoro");

        if (tracking.WatchedEpisodes is { } ep)
        {
            request.WithEpisodesWatched(ep);
        }

        if (tracking.Status is { } status)
        {
            if(status == AnimeStatus.Rewatching)
            {
                request.WithIsRewatching(true);
            }
            else
            {
                request.WithStatus((MalApi.AnimeStatus)(int)status);
            }
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

        var response = await request.Publish();
        var newTracking = new Tracking
        {
            WatchedEpisodes = response.WatchedEpisodes,
            Status = (AnimeStatus)(int)response.Status,
            Score = (int)response.Score,
            UpdatedAt = response.UpdatedAt
        };
        
        this.Log().Debug("Tracking Updated {0}.", newTracking);
        return newTracking;
    }

    public async Task<bool> Delete(long id)
    {
        return await _client.Anime().WithId(id).RemoveFromList();
    }

    public async Task<User> GetUser()
    {
        var user = await _client.User();
        return new User { Name = user.Name, Image = user.PictureUri };
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
}
