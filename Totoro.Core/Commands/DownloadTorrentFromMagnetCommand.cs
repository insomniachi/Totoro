using MediatR;

namespace Totoro.Core.Commands;

public class DownloadTorrentFromMagnetCommand : IRequest
{
    required public string Title { get; init; }
    required public string Magnet { get; init; }
}
