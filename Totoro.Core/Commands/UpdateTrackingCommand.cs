using MediatR;

namespace Totoro.Core.Commands;

public class UpdateTrackingCommand : IRequest
{
    required public IAnimeModel Anime { get; init; }
    required public Tracking Tracking { get; init; }
}
