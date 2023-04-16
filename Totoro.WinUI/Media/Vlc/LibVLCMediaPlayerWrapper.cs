﻿using System.Diagnostics;
using System.Reactive.Subjects;
using System.Text.Json;
using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using ReactiveMarbles.ObservableEvents;

namespace Totoro.WinUI.Media.Vlc;

internal class LibVLCMediaPlayerWrapper : IMediaPlayer
{
    private LibVLC _vlc;
    private MediaPlayer _mp;
    private readonly Subject<Unit> _paused = new();
    private readonly Subject<Unit> _playing = new();
    private readonly Subject<Unit> _ended = new();
    private readonly Subject<TimeSpan> _durationChanged = new();
    private readonly Subject<TimeSpan> _positionChanged = new();

    public IObservable<Unit> Paused => _paused;
    public IObservable<Unit> Playing => _playing;
    public IObservable<Unit> PlaybackEnded => _ended;
    public IObservable<TimeSpan> PositionChanged => _positionChanged;
    public IObservable<TimeSpan> DurationChanged => _durationChanged;
    public IMediaTransportControls TransportControls { get; } = new VlcMediaTransportControls();
    public bool IsInitialized { get; private set; }
    public MediaPlayerType Type => MediaPlayerType.Vlc;

    public void Dispose()
    {
        _mp?.Pause();
    }

    public void Pause()
    {
        _mp?.Pause();
    }

    public void Play()
    {
        _mp?.Play();
    }

    public void Play(double offsetInSeconds)
    {
        _mp?.Play();
        _mp?.SeekTo(TimeSpan.FromSeconds(offsetInSeconds));
    }

    public void SeekTo(TimeSpan ts)
    {
        _mp?.SeekTo(ts);
    }

    public void Seek(TimeSpan ts, SeekDirection direction)
    {
        var sign = direction switch
        {
            SeekDirection.Forward => 1,
            SeekDirection.Backward => -1,
            _ => throw new UnreachableException()
        };

        _mp.Time += (sign * (long)ts.TotalMilliseconds);
    }

    public async Task<Unit> SetMedia(VideoStreamModel stream)
    {
        while(_vlc is null)
        {
            await Task.Delay(10);
        }

        var media = stream.Stream is null
            ? new LibVLCSharp.Shared.Media(_vlc, stream.StreamUrl, FromType.FromLocation)
            : new LibVLCSharp.Shared.Media(_vlc, new StreamMediaInput(stream.Stream));

        _mp.Media = media;

        if (stream.AdditionalInformation?.TryGetValue("subtitles", out string json) == true)
        {
            var subtitles = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            var count = 0;
            foreach (var item in subtitles)
            {
                _mp.AddSlave(MediaSlaveType.Subtitle, item.Value, count++ == 0);
            }
        }

        return Unit.Default;
    }

    public void Initialize(LibVLC vlc, MediaPlayer mp, VideoView videoView)
    {
        _vlc = vlc;
        _mp = mp;

        var vlcTransport = TransportControls as VlcMediaTransportControls;
        vlcTransport.Initialize(vlc, mp, videoView);

        _mp.Events()
           .Paused
           .Subscribe(_ => _paused.OnNext(Unit.Default));

        _mp.Events()
           .Playing
           .Subscribe(_ => _playing.OnNext(Unit.Default));

        _mp.Events()
           .TimeChanged
           .Select(x => TimeSpan.FromMilliseconds(x.Time))
           .Subscribe(_positionChanged.OnNext);

        _mp.Events()
           .LengthChanged
           .Where(x => x.Length > 0)
           .Select(x => TimeSpan.FromMilliseconds(x.Length))
           .Subscribe(_durationChanged.OnNext);

        _mp.Events()
           .EndReached
           .Subscribe(_ => _ended.OnNext(Unit.Default));

        IsInitialized = true;
    }
}
