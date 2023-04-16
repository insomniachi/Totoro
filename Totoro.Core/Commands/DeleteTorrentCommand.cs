using MediatR;

namespace Totoro.Core.Commands;

public class DeleteTorrentCommand : IRequest
{
    required public string Name { get; init; }
    required public bool DeleteFiles { get; init; }
}
