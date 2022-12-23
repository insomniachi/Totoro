
using Totoro.WinUI.Media;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class SubmitTimeStampsViewModel : DialogViewModel
{
    private IDisposable _subscription;
    private readonly ITimestampsService _timestampsService;

    public SubmitTimeStampsViewModel(ITimestampsService timestampsService)
    {
        _timestampsService = timestampsService;

        PlayRange = ReactiveCommand.Create(() => Play());
        SetStartPosition = ReactiveCommand.Create(() => StartPosition = MediaPlayer.GetMediaPlayer().Position.TotalSeconds);
        SetEndPosition = ReactiveCommand.Create(() => EndPosition = MediaPlayer.GetMediaPlayer().Position.TotalSeconds);
        SkipNearEnd = ReactiveCommand.Create(() => MediaPlayer.Seek(TimeSpan.FromSeconds(EndPosition - 5)));
        Submit = ReactiveCommand.CreateFromTask(SubmitTimeStamp);

        MediaPlayer
            .PositionChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => CurrentPlayerPosition = x.TotalSeconds);

        this.WhenAnyValue(x => x.StartPosition)
            .DistinctUntilChanged()
            .Subscribe(x => MediaPlayer.Seek(TimeSpan.FromSeconds(x)));

        this.WhenAnyValue(x => x.SelectedTimeStampType)
            .Subscribe(type =>
            {
                if (type == "OP")
                {
                    StartPosition = SuggestedStartPosition;
                }
                else if (type == "ED")
                {
                    StartPosition = SuggestedEndPosition;
                }
                EndPosition = StartPosition + 85;
            });

        this.WhenAnyValue(x => x.Stream)
            .WhereNotNull()
            .Subscribe(MediaPlayer.SetMedia);
    }

    [Reactive] public double StartPosition { get; set; }
    [Reactive] public double EndPosition { get; set; }
    [Reactive] public string SelectedTimeStampType { get; set; } = "OP";
    [Reactive] public double CurrentPlayerPosition { get; set; }
    [Reactive] public VideoStream Stream { get; set; }
    public long MalId { get; set; }
    public int Episode { get; set; }
    public double Duration { get; set; }
    public string[] TimeStampTypes = new[] { "OP", "ED" };
    public double SuggestedStartPosition { get; set; }
    public double SuggestedEndPosition => Duration - 120;
    public WinUIMediaPlayerWrapper MediaPlayer { get; } = new WinUIMediaPlayerWrapper();

    public ICommand PlayRange { get; }
    public ICommand SetStartPosition { get; }
    public ICommand SetEndPosition { get; }
    public ICommand SkipNearEnd { get; }
    public ICommand Submit { get; }

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

    public async Task SubmitTimeStamp()
    {
        await _timestampsService.SubmitTimeStamp(MalId, Episode, SelectedTimeStampType.ToLower(),  new Interval { StartTime = StartPosition, EndTime = EndPosition }, Duration);
    }

}
