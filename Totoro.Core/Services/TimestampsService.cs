using System.Text.Json.Nodes;
using Refit;
using Splat;
using Totoro.Core.Services.Aniskip;
using Interval = Totoro.Core.Models.Interval;

namespace Totoro.Core.Services;

internal class TimestampsService : ITimestampsService, IEnableLogger
{
    private readonly ISettings _settings;
    private readonly IAnimeIdService _animeIdService;
    private readonly IAniskipClient _aniskipClient;

    public TimestampsService(ISettings settings,
                             IAnimeIdService animeIdService,
                             IAniskipClient aniskipClient)
    {
        _settings = settings;
        _animeIdService = animeIdService;
        _aniskipClient = aniskipClient;
    }

    public async Task<TimestampResult> GetTimeStampsWithMalId(long malId, int ep, double duration)
    {
        try
        {
            var query = new GetSkipTimesQueryV2
            {
                Types = new[] { SkipType.Opening, SkipType.Ending, SkipType.Recap },
                EpisodeLength = duration
            };
            var result = await _aniskipClient.GetSkipTimes(malId, ep, query);
            return new TimestampResult
            {
                Success = result.IsFound,
                Items = result.Results.Select(x => new Timestamp
                {
                    Interval = new Interval { StartTime = x.Interval.StartTime, EndTime = x.Interval.EndTime },
                    SkipId = x.SkipId,
                    SkipType = x.SkipType.ToString(),
                    EpisodeLength = x.EpisodeLength
                }).ToArray(),
            };
        }
        catch (ApiException)
        {
            this.Log().Info($"Timestamps for MalId = {malId}, Ep = {ep}, Duration = {duration} not found");
        }

        return new TimestampResult { Success = false, Items = Array.Empty<Timestamp>() };
    }

    public async Task<TimestampResult> GetTimeStamps(long id, int ep, double duration)
    {
        var malId = await GetMalId(id);
        return await GetTimeStampsWithMalId(malId, ep, duration);
    }

    public async Task SubmitTimeStampWithMalId(long malId, int ep, string skipType, Interval interval, double episodeLength)
    {
        try
        {
            var payload = new PostCreateSkipTimeRequestBodyV2
            {
                SkipType = skipType == "op" ? SkipType.Opening : SkipType.Ending,
                ProviderName = _settings.DefaultProviderType,
                StartTime = interval.StartTime,
                EndTime = interval.EndTime,
                EpisodeLength = episodeLength,
                SubmitterId = _settings.AniSkipId
            };

            await _aniskipClient.PostSkipTimes(malId, ep, payload);
        }
        catch (ApiException ex)
        {
            this.Log().Error(ex);
        }
    }

    public async Task SubmitTimeStamp(long id, int ep, string skipType, Interval interval, double episodeLength)
    {
        var malId = await GetMalId(id);
        await SubmitTimeStampWithMalId(malId, ep, skipType, interval, episodeLength);
    }

    public async Task Vote(Guid skipId, bool isThumpsUp)
    {
        try
        {
            var payload = new PostVoteRequestBodyV2
            {
                VoteType = isThumpsUp ? VoteType.Upvote : VoteType.Downvote
            };
            await _aniskipClient.PostVote(skipId, payload);
        }
        catch (ApiException ex)
        {
            this.Log().Error(ex);
        }
    }

    private async ValueTask<long> GetMalId(long id)
    {
        if (_settings.DefaultListService == ListServiceType.MyAnimeList)
        {
            return id;
        }
        else
        {
            return (await _animeIdService.GetId(id))?.MyAnimeList ?? 0;
        }
    }
}

