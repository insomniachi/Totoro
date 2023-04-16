using MediatR;
using Totoro.Core.Commands;

namespace Totoro.Core.CommandHandlers;

internal class DeleteTorrentCommandHandler : IRequestHandler<DeleteTorrentCommand>
{
    private readonly ITorrentEngine _torrentEngine;

    public DeleteTorrentCommandHandler(ITorrentEngine torrentEngine)
    {
        _torrentEngine = torrentEngine;
    }

    public Task Handle(DeleteTorrentCommand request, CancellationToken cancellationToken) => _torrentEngine.RemoveTorrent(request.Name, request.DeleteFiles);
}
