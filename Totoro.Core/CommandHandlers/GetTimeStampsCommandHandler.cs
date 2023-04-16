using MediatR;
using Totoro.Core.Commands;

namespace Totoro.Core.CommandHandlers;

internal class GetTimeStampsCommandHandler : IRequestHandler<GetTimeStampsCommand, AniSkipResult>
{
    private readonly ITimestampsService _timestampsService;

    public GetTimeStampsCommandHandler(ITimestampsService timestampsService)
    {
        _timestampsService = timestampsService;
    }

    public Task<AniSkipResult> Handle(GetTimeStampsCommand request, CancellationToken cancellationToken)
        => _timestampsService.GetTimeStamps(request.MalId, request.EpisodeNumber, request.EpisodeDuration);
}
