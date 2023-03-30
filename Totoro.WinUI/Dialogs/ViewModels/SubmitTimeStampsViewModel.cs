using Totoro.WinUI.Media.Wmp;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class SubmitTimeStampsViewModel : DialogViewModel
{
    private IDisposable _subscription;
    private readonly ITimestampsService _timestampsService;

    public SubmitTimeStampsViewModel(ITimestampsService timestampsService)
    {
        _timestampsService = timestampsService;

        PlayRange = ReactiveCommand.Create(Play);
        SetStartPosition = ReactiveCommand.Create(() => StartPosition = MediaPlayer.GetMediaPlayer().Position.TotalSeconds);
        SetEndPosition = ReactiveCommand.Create(() => EndPosition = MediaPlayer.GetMediaPlayer().Position.TotalSeconds);
        SkipNearEnd = ReactiveCommand.Create(() => MediaPlayer.SeekTo(TimeSpan.FromSeconds(EndPosition - 5)));
        Submit = ReactiveCommand.CreateFromTask(SubmitTimeStamp);

        var canVote = this.WhenAnyValue(x => x.SelectedTimeStampType, x => x.ExistingResult)
            .Where(x => x.Item2 is not null)
            .Select(tuple =>
            {
                (string type, AniSkipResult result) = tuple;
                return type switch
                {
                    "OP" => result.Opening is not null,
                    "ED" => result.Ending is not null,
                    _ => false
                };
            });

        VoteUp = ReactiveCommand.Create(() => Vote(true), canVote);
        VoteDown = ReactiveCommand.Create(() => Vote(false), canVote);

        MediaPlayer
            .PositionChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => CurrentPlayerPosition = x.TotalSeconds);

        this.WhenAnyValue(x => x.StartPosition)
            .DistinctUntilChanged()
            .Subscribe(x => MediaPlayer.SeekTo(TimeSpan.FromSeconds(x)));

        this.WhenAnyValue(x => x.SelectedTimeStampType, x => x.ExistingResult)
            .Subscribe(tuple =>
            {
                (string type, AniSkipResult result) = tuple;

                if (type == "OP")
                {
                    StartPosition = result?.Opening?.Interval?.StartTime ?? SuggestedStartPosition;
                    EndPosition = result?.Opening?.Interval?.EndTime ?? StartPosition + 90;
                }
                else if (type == "ED")
                {
                    StartPosition = result?.Ending?.Interval?.StartTime ?? SuggestedEndPosition;
                    EndPosition = result?.Ending?.Interval?.EndTime ?? StartPosition + 90;
                }
            });

        this.WhenAnyValue(x => x.Stream)
            .WhereNotNull()
            .Subscribe(async x => await MediaPlayer.SetMedia(x));
    }

    [Reactive] public double StartPosition { get; set; }
    [Reactive] public double EndPosition { get; set; }
    [Reactive] public string SelectedTimeStampType { get; set; } = "OP";
    [Reactive] public double CurrentPlayerPosition { get; set; }
    [Reactive] public VideoStreamModel Stream { get; set; }
    [Reactive] public AniSkipResult ExistingResult { get; set; }
    public long MalId { get; set; }
    public int Episode { get; set; }
    public double Duration { get; set; }
    public string[] TimeStampTypes = new[] { "OP", "ED" };
    public double SuggestedStartPosition { get; set; }
    public double SuggestedEndPosition => Duration - 120;
    public WinUIMediaPlayerWrapper MediaPlayer { get; } = App.GetService<IMediaPlayerFactory>().Create(MediaPlayerType.WindowsMediaPlayer) as WinUIMediaPlayerWrapper;

    public ICommand PlayRange { get; }
    public ICommand SetStartPosition { get; }
    public ICommand SetEndPosition { get; }
    public ICommand SkipNearEnd { get; }
    public ICommand Submit { get; }
    public ICommand VoteUp { get; }
    public ICommand VoteDown { get; }

    private void Play()
    {
        _subscription?.Dispose();
        MediaPlayer.Play(StartPosition);

        _subscription = this.WhenAnyValue(x => x.CurrentPlayerPosition)
            .Where(time => time >= EndPosition)
            .Subscribe(_ =>
            {
                _subscription?.Dispose();
                MediaPlayer.Pause();
            });
    }

    private void Vote(bool vote)
    {
        if (SelectedTimeStampType == "OP" && ExistingResult.Opening is { } op)
        {
            _timestampsService.Vote(op.SkipId, vote);
        }
        else if (SelectedTimeStampType == "ED" && ExistingResult.Ending is { } ed)
        {
            _timestampsService.Vote(ed.SkipId, vote);
        }
    }

    public async Task SubmitTimeStamp()
    {
        await _timestampsService.SubmitTimeStamp(MalId, Episode, SelectedTimeStampType.ToLower(), new Interval { StartTime = StartPosition, EndTime = EndPosition }, Duration);
    }

}
