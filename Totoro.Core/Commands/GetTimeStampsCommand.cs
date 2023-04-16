using MediatR;

namespace Totoro.Core.Commands;

public class GetTimeStampsCommand : IRequest<AniSkipResult>
{
    required public long MalId { get; init; }
    required public int EpisodeNumber { get; init; }
    required public double EpisodeDuration { get; init; }
}
