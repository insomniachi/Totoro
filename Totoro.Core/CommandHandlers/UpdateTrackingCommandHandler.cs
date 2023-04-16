using MediatR;
using Totoro.Core.Commands;

namespace Totoro.Core.CommandHandlers;

internal class UpdateTrackingCommandHandler : IRequestHandler<UpdateTrackingCommand>
{
    private readonly ITrackingServiceContext _trackingServiceContext;

    public UpdateTrackingCommandHandler(ITrackingServiceContext trackingServiceContext)
    {
        _trackingServiceContext = trackingServiceContext;
    }

    public async Task Handle(UpdateTrackingCommand request, CancellationToken cancellationToken)
    {
        request.Anime.Tracking = await _trackingServiceContext.Update(request.Anime.Id, request.Tracking);
    }
}
