using System.Reactive.Windows.Foundation;
using MediaPlayerElementWithHttpClient;
using ReactiveMarbles.ObservableEvents;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Streaming.Adaptive;
using Windows.Storage;
using Windows.Web.Http;

namespace Totoro.WinUI.Media;

public sealed class WinUIMediaPlayerWrapper : IMediaPlayer
{
    private readonly MediaPlayer _player = new();
    private readonly HttpClient _httpClient = new();


    public IObservable<Unit> Paused => _player.Events().CurrentStateChanged.Where(x => x.sender.CurrentState == MediaPlayerState.Paused).Select(_ => Unit.Default);

    public IObservable<Unit> Playing => _player.Events().CurrentStateChanged.Where(x => x.sender.CurrentState == MediaPlayerState.Playing).Select(_ => Unit.Default);

    public IObservable<Unit> PlaybackEnded => _player.Events().MediaEnded.Select(_ => Unit.Default);

    public IObservable<TimeSpan> PositionChanged => _player.PlaybackSession.Events().PositionChanged.Select(x => x.sender.Position);

    public IObservable<TimeSpan> DurationChanged => _player.PlaybackSession.Events().NaturalDurationChanged.Select(x => x.sender.NaturalDuration);

    public void Dispose() => _player.Pause();

    public void Pause() => _player.Pause();

    public void Play() => _player.Play();

    public void Seek(TimeSpan ts) => _player.Position = ts;

    public void Play(double offsetInSeconds)
    {
        _player.Position = TimeSpan.FromSeconds(offsetInSeconds);
        _player.Play();
    }

    public void SetMedia(VideoStream stream)
    {
        if (stream.Headers is { Count: > 0 } headers)
        {
            _httpClient.DefaultRequestHeaders.Clear();
            foreach (var item in stream.Headers)
            {
                _httpClient.DefaultRequestHeaders.Add(item.Key, item.Value);
            }

            Uri uri = new(stream.Url);
            AdaptiveMediaSource.CreateFromUriAsync(uri, _httpClient)
                .ToObservable()
                .Subscribe(async amsr =>
                {
                    if (amsr.Status == AdaptiveMediaSourceCreationStatus.Success)
                    {
                        _player.Source = MediaSource.CreateFromAdaptiveMediaSource(amsr.MediaSource);
                    }
                    else
                    {
                        HttpRandomAccessStream httpStream = await HttpRandomAccessStream.CreateAsync(_httpClient, uri);
                        _player.Source = MediaSource.CreateFromStream(httpStream, httpStream.ContentType);
                    }
                });
        }
        else
        {
            _player.Source = MediaSource.CreateFromUri(new Uri(stream.Url));
        }
    }

    public async Task SetMedia(string localFile)
    {
        _player.Source = MediaSource.CreateFromStorageFile(await StorageFile.GetFileFromPathAsync(localFile));
    }

    public MediaPlayer GetMediaPlayer() => _player;
}
