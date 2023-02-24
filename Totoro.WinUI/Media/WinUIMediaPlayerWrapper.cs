using System.Text.Json;
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
    private readonly Dictionary<TimedTextSource, string> _ttsMap = new();
    private bool _isHardSub;


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

    public async Task<Unit> SetMedia(VideoStream stream, Dictionary<string, string> AdditionalInformation = null)
    {
        var source = await GetMediaSource(stream);
        _isHardSub = stream.Quality == "hardsub";

        if (AdditionalInformation?.TryGetValue("subtitles", out string json) == true)
        {
            var subtitles = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            _ttsMap.Clear();
            foreach (var item in subtitles)
            {
                var tts = TimedTextSource.CreateFromUri(new Uri(item.Value));
                tts.Resolved += OnTtsResolved;
                source.ExternalTimedTextSources.Add(tts);
                _ttsMap[tts] = item.Key;
            }
        }

        _player.Source = new MediaPlaybackItem(source);

        return Unit.Default;
    }

    private void OnTtsResolved(TimedTextSource sender, TimedTextSourceResolveResultEventArgs args)
    {
        if (!_ttsMap.TryGetValue(sender, out string lang))
        {
            return;
        }

        args.Tracks[0].Label = lang;

        if (lang == "en-US" && !_isHardSub)
        {
            var index = args.Tracks[0].PlaybackItem.TimedMetadataTracks.IndexOf(args.Tracks[0]);
            args.Tracks[0].PlaybackItem.TimedMetadataTracks.SetPresentationMode((uint)index, TimedMetadataTrackPresentationMode.PlatformPresented);
        }
    }

    public async Task<Unit> SetMediaFromFile(string localFile)
    {
        _player.Source = MediaSource.CreateFromStorageFile(await StorageFile.GetFileFromPathAsync(localFile));
        return Unit.Default;
    }

    public MediaPlayer GetMediaPlayer() => _player;

    private async Task<MediaSource> GetMediaSource(VideoStream stream)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        var hasHeaders = stream.Headers is { Count: > 0 };
        Uri uri = new(stream.Url);
        
        if (hasHeaders)
        {
            foreach (var item in stream.Headers)
            {
                _httpClient.DefaultRequestHeaders.Add(item.Key, item.Value);
            }
            var result = await AdaptiveMediaSource.CreateFromUriAsync(uri, _httpClient);
            if (result.Status == AdaptiveMediaSourceCreationStatus.Success)
            {
                return MediaSource.CreateFromAdaptiveMediaSource(result.MediaSource);
            }
            else
            {
                HttpRandomAccessStream httpStream = await HttpRandomAccessStream.CreateAsync(_httpClient, uri);
                return MediaSource.CreateFromStream(httpStream, httpStream.ContentType);
            }
        }
        else
        {
            return MediaSource.CreateFromUri(uri);
        }

    }

    public void SetMedia(string url)
    {
        _player.Source = MediaSource.CreateFromUri(new Uri(url));
    }
}
