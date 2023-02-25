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
    private FFmpegInteropX.FFmpegMediaSource _ffmpegMediaSource;


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

    public async Task SetSubtitleFromFile(string file)
    {
        var sf = await StorageFile.GetFileFromPathAsync(file);
        var tts = TimedTextSource.CreateFromStream(await sf.OpenReadAsync());
        tts.Resolved += OnTtsResolved;
        var source = _player.Source as MediaSource;
        source.ExternalTimedTextSources.Add(tts);
    }

    private void OnTtsResolved(TimedTextSource sender, TimedTextSourceResolveResultEventArgs args)
    {
        if (!_ttsMap.TryGetValue(sender, out string lang))
        {
            var index = args.Tracks[0].PlaybackItem.TimedMetadataTracks.IndexOf(args.Tracks[0]);
            args.Tracks[0].PlaybackItem.TimedMetadataTracks.SetPresentationMode((uint)index, TimedMetadataTrackPresentationMode.PlatformPresented);
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

    public async Task SetMedia(string url)
    {
        _ffmpegMediaSource = await FFmpegInteropX.FFmpegMediaSource.CreateFromUriAsync(url, new FFmpegInteropX.MediaSourceConfig
        {
            ReadAheadBufferEnabled = true,
            ReadAheadBufferDuration = TimeSpan.FromSeconds(30),
            ReadAheadBufferSize = 50 * 1024 * 1024,
            FFmpegOptions = new Windows.Foundation.Collections.PropertySet
            {
                { "reconnect", 1 },
                { "reconnect_streamed", 1 },
                { "reconnect_on_network_error", 1 },
            }
        });
        
        var mediaSource = _ffmpegMediaSource.CreateMediaPlaybackItem();
        mediaSource.TimedMetadataTracks.SetPresentationMode(0, TimedMetadataTrackPresentationMode.PlatformPresented);
        _player.Source = mediaSource;
    }
}
