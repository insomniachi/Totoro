using System.Diagnostics;
using Totoro.WinUI.Media.Flyleaf;
using Totoro.WinUI.Media.Wmp;

namespace Totoro.WinUI.Media
{
    internal class MediaPlayerFactory : IMediaPlayerFactory
    {
        private readonly Func<WinUIMediaPlayerWrapper> _windowMediaPlayerCreator;
        private readonly Func<FlyleafMediaPlayerWrapper> _flyleafMediaPlayerCreator;

        public MediaPlayerFactory(Func<WinUIMediaPlayerWrapper> windowMediaPlayerCreator,
                                  Func<FlyleafMediaPlayerWrapper> flyleafMediaPlayerCreator)
        {
            _windowMediaPlayerCreator = windowMediaPlayerCreator;
            _flyleafMediaPlayerCreator = flyleafMediaPlayerCreator;
        }

        public IMediaPlayer Create(MediaPlayerType type)
        {
            return type switch
            {
                MediaPlayerType.WindowsMediaPlayer => _windowMediaPlayerCreator(),
                MediaPlayerType.FFMpeg => _flyleafMediaPlayerCreator(),
                _ => throw new UnreachableException()
            };
        }
    }
}
