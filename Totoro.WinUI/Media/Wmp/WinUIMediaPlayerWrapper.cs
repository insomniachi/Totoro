using System.Diagnostics;
using System.IO;
using System.Text.Json;
using ReactiveMarbles.ObservableEvents;
using Splat;
using Totoro.Plugins.Anime.Models;
using Totoro.WinUI.Contracts;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Streaming.Adaptive;
using Windows.Storage;
using Windows.Web.Http;

namespace Totoro.WinUI.Media.Wmp;

public sealed class WinUIMediaPlayerWrapper : IMediaPlayer, IEnableLogger
{
    private readonly CustomMediaTransportControls _transportControls;
    private readonly MediaPlayer _player = new();
    private readonly HttpClient _httpClient = new();
    private readonly Dictionary<TimedTextSource, string> _ttsMap = new();
    private bool _isHardSub;
    private bool _isDisposed;

    public WinUIMediaPlayerWrapper(IWindowService windowService)
    {
        _transportControls = new CustomMediaTransportControls(windowService);
    }

    public IObservable<Unit> Paused => _player.Events().CurrentStateChanged.Where(x => x.sender.CurrentState == MediaPlayerState.Paused).Select(_ => Unit.Default);
    public IObservable<Unit> Playing => _player.Events().CurrentStateChanged.Where(x => x.sender.CurrentState == MediaPlayerState.Playing).Select(_ => Unit.Default);
    public IObservable<Unit> PlaybackEnded => _player.Events().MediaEnded.Select(_ => Unit.Default);
    public IObservable<TimeSpan> PositionChanged => _player.PlaybackSession.Events().PositionChanged.Select(x => x.sender.Position);
    public IObservable<TimeSpan> DurationChanged => _player.PlaybackSession.Events().NaturalDurationChanged.Select(x => x.sender.NaturalDuration);
    public IMediaTransportControls TransportControls => _transportControls;
    public MediaPlayerType Type => MediaPlayerType.WindowsMediaPlayer;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _player.Pause();
        _isDisposed = true;
    }
    public void Pause()
    {
        if (_isDisposed)
        {
            return;
        }

        _player.Pause();
    }
    public void Play()
    {
        if (_isDisposed)
        {
            return;
        }

        _player.Play();
    }
    public void SeekTo(TimeSpan ts)
    {
        if (_isDisposed)
        {
            return;
        }

        _player.Position = ts;
    }

    public void Seek(TimeSpan ts, SeekDirection direction)
    {
        if (_isDisposed)
        {
            return;
        }

        var sign = direction switch
        {
            SeekDirection.Forward => 1,
            SeekDirection.Backward => -1,
            _ => throw new UnreachableException()
        };

        _player.Position += ts * sign;
    }

    public void Play(double offsetInSeconds)
    {
        if (_isDisposed)
        {
            return;
        }

        _player.Position = TimeSpan.FromSeconds(offsetInSeconds);
        _player.Play();
    }

    private void SetSubtitles(MediaSource source, AdditionalVideoStreamInformation additionalVideoStreamInformation)
    {
        //if (AdditionalInformation?.TryGetValue("subtitles", out string json) == true)
        //{
        //    var subtitles = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        //    _ttsMap.Clear();
        //    foreach (var item in subtitles)
        //    {
        //        var tts = TimedTextSource.CreateFromUri(new Uri(item.Value));
        //        tts.Resolved += OnTtsResolved;
        //        source.ExternalTimedTextSources.Add(tts);
        //        _ttsMap[tts] = item.Key;
        //    }
        //}

        //if (AdditionalInformation?.TryGetValue("subtitleFiles", out string jsonFiles) == true)
        //{
        //    var subtitles = JsonSerializer.Deserialize<List<KeyValuePair<string, string>>>(jsonFiles);
        //    _ttsMap.Clear();
        //    foreach (var item in subtitles)
        //    {
        //        _ = SetSubtitleFromFile(source, item.Value);
        //    }
        //}

    }

    public async Task<Unit> SetMedia(VideoStreamModel stream)
    {
        MediaSource source;

        if (stream.Stream is not null)
        {
            source = MediaSource.CreateFromStream(stream.Stream.AsRandomAccessStream(), "video/x-matroska");
        }
        else
        {
            source = await GetMediaSource(stream.StreamUrl, stream.Headers);
        }

        _isHardSub = stream.Resolution == "hardsub";
        SetSubtitles(source, stream.AdditionalInformation);
        _player.Source = new MediaPlaybackItem(source);
        return Unit.Default;
    }

    public async Task SetSubtitleFromFile(MediaSource source, string file)
    {
        var fileName = Path.GetFileName(file);
        var sf = await StorageFile.GetFileFromPathAsync(file);
        var tts = TimedTextSource.CreateFromStream(await sf.OpenReadAsync());
        _ttsMap[tts] = fileName.ToLower();
        tts.Resolved += OnTtsResolved;
        source.ExternalTimedTextSources.Add(tts);
    }

    private void OnTtsResolved(TimedTextSource sender, TimedTextSourceResolveResultEventArgs args)
    {
        if (!_ttsMap.TryGetValue(sender, out string lang))
        {
            return;
        }

        args.Tracks[0].Label = lang;

        if (!_isHardSub && (lang == "en-US" ||
            lang.Contains("english") ||
            lang.Contains("eng")))
        {
            var index = args.Tracks[0].PlaybackItem.TimedMetadataTracks.IndexOf(args.Tracks[0]);
            args.Tracks[0].PlaybackItem.TimedMetadataTracks.SetPresentationMode((uint)index, TimedMetadataTrackPresentationMode.PlatformPresented);
        }
    }

    public MediaPlayer GetMediaPlayer() => _player;

    private async Task<MediaSource> GetMediaSource(string url, Dictionary<string, string> headers)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        var hasHeaders = headers is { Count: > 0 };
        Uri uri = new(url);

        if (hasHeaders)
        {
            foreach (var item in headers)
            {
                _httpClient.DefaultRequestHeaders.Add(item.Key, item.Value);
            }

            var result = await AdaptiveMediaSource.CreateFromUriAsync(uri, _httpClient);
            if (result.Status == AdaptiveMediaSourceCreationStatus.Success)
            {
                this.Log().Info("Creating adaptive media from {0}", uri);
                this.Log().Info("With headers {0}", string.Join(";", headers.Select(x => $"{x.Key}={x.Value}")));

                return MediaSource.CreateFromAdaptiveMediaSource(result.MediaSource);
            }
            else
            {
                this.Log().Info("Creating media from http stream");
                HttpRandomAccessStream httpStream = await HttpRandomAccessStream.CreateAsync(_httpClient, uri);
                return MediaSource.CreateFromStream(httpStream, httpStream.ContentType);
            }
        }
        else
        {
            this.Log().Info("Creating media from : {0}", uri);

            return MediaSource.CreateFromUri(uri);
        }
    }
}
