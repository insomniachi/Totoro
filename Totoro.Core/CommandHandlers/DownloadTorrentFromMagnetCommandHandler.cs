using MediatR;
using Totoro.Core.Commands;

namespace Totoro.Core.CommandHandlers;

internal class DownloadTorrentFromMagnetCommandHandler : IRequestHandler<DownloadTorrentFromMagnetCommand>
{
    private readonly ISettings _settings;
    private readonly ITorrentEngine _torrentEngine;

    public DownloadTorrentFromMagnetCommandHandler(ISettings settings,
                                                   ITorrentEngine torrentEngine)
    {
        _settings = settings;
        _torrentEngine = torrentEngine;
    }

    public async Task Handle(DownloadTorrentFromMagnetCommand request, CancellationToken cancellationToken)
    {
        var title = AnitomySharp.AnitomySharp.Parse(request.Title).First(x => x.Category == AnitomySharp.Element.ElementCategory.ElementAnimeTitle).Value;
        await _torrentEngine.DownloadFromMagnet(request.Magnet, Path.Combine(_settings.UserTorrentsDownloadDirectory, title));
    }
}
