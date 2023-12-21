using System.Diagnostics;
using Totoro.WinUI.Media.Flyleaf;
using Totoro.WinUI.Media.Vlc;
using Totoro.WinUI.Media.Wmp;

namespace Totoro.WinUI.Media
{
    internal class MediaPlayerFactory : IMediaPlayerFactory
    {
        private readonly Func<WinUIMediaPlayerWrapper> _windowMediaPlayerCreator;
        private readonly Func<LibVLCMediaPlayerWrapper> _vlcMediaPlayerCreator;
        private readonly Func<FlyleafMediaPlayerWrapper> _flyleafMediaPlayerCreator;

        public MediaPlayerFactory(Func<WinUIMediaPlayerWrapper> windowMediaPlayerCreator,
                                  Func<LibVLCMediaPlayerWrapper> vlcMediaPlayerCreator,
                                  Func<FlyleafMediaPlayerWrapper> flyleafMediaPlayerCreator)
        {
            _windowMediaPlayerCreator = windowMediaPlayerCreator;
            _vlcMediaPlayerCreator = vlcMediaPlayerCreator;
            _flyleafMediaPlayerCreator = flyleafMediaPlayerCreator;
        }

        public IMediaPlayer Create(MediaPlayerType type)
        {
            return type switch
            {
                MediaPlayerType.WindowsMediaPlayer => _windowMediaPlayerCreator(),
                MediaPlayerType.Vlc => _vlcMediaPlayerCreator(),
                MediaPlayerType.FFMpeg => _flyleafMediaPlayerCreator(),
                _ => throw new UnreachableException()
            };
        }
    }
}
