using System.Diagnostics;
using Totoro.WinUI.Media.Vlc;
using Totoro.WinUI.Media.Wmp;

namespace Totoro.WinUI.Media
{
    internal class MediaPlayerFactory : IMediaPlayerFactory
    {
        private readonly Func<WinUIMediaPlayerWrapper> _windowMediaPlayerCreator;
        private readonly Func<LibVLCMediaPlayerWrapper> _vlcMediaPlayerCreator;

        public MediaPlayerFactory(Func<WinUIMediaPlayerWrapper> windowMediaPlayerCreator,
                                  Func<LibVLCMediaPlayerWrapper> vlcMediaPlayerCreator)
        {
            _windowMediaPlayerCreator = windowMediaPlayerCreator;
            _vlcMediaPlayerCreator = vlcMediaPlayerCreator;
        }

        public IMediaPlayer Create(MediaPlayerType type)
        {
            return type switch
            {
                MediaPlayerType.WindowsMediaPlayer => _windowMediaPlayerCreator(),
                MediaPlayerType.Vlc => _vlcMediaPlayerCreator(),
                _ => throw new UnreachableException()
            };
        }
    }
}
