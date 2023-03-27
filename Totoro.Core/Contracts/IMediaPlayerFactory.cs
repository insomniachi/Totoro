namespace Totoro.Core.Contracts;

public interface IMediaPlayerFactory
{
    IMediaPlayer Create(MediaPlayerType type);
}

