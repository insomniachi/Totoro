using Refit;

namespace Totoro.Core.Services.Aniskip;

internal interface IAniskipClient
{
    [Get("/v2/skip-times/{animeId}/{episodeNumber}")]
    Task<GetSkipTimesResponseV2> GetSkipTimes(long animeId, double episodeNumber, GetSkipTimesQueryV2 query);

    [Post("/v2/skip-times/{animeId}/{episodeNumber}")]
    Task<PostCreateSkipTimeResponseV2> PostSkipTimes(long animeId, double episodeNumber, [Body] PostCreateSkipTimeRequestBodyV2 payload);

    [Post("/v2/skip-times/vote/{skipId}")]
    Task<PostVoteResponseV2> PostVote(Guid skipId, [Body] PostVoteRequestBodyV2 payload);
}