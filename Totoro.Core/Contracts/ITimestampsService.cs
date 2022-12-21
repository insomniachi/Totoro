﻿using Totoro.Core.Services;

namespace Totoro.Core.Contracts
{
    public interface ITimestampsService
    {
        Task<AnimeTimeStamps> GetTimeStamps(long malId);
        Task<AniSkipResult> GetTimeStamps(long malId, int ep, double duration);
        Task SubmitTimeStamp(long malId, int ep, string skipType, Interval interval, double episodeLength);
    }
}