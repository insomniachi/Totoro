
using Totoro.WinUI.Media;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class SubmitTimeStampsViewModel : DialogViewModel
{
    private IDisposable _subscription;
    public SubmitTimeStampsViewModel(ITimestampsService timestampsService)
    {
        _timestampsService = timestampsService;

        PlayRange = ReactiveCommand.Create(() => Play());

        MediaPlayer
            .PositionChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => CurrentPlayerPosition = x.TotalSeconds);
    }

    [Reactive] public double StartPosition { get; set; }
    [Reactive] public double EndPosition { get; set; }
    [Reactive] public string SelectedTimeStampType { get; set; } = "OP";
    [Reactive] public double CurrentPlayerPosition { get; set; }

    public string MediaUrl { get; set; }
    public long MalId { get; set; }
    public int Episode { get; set; }
    public double Duration { get; set; }
    public string[] TimeStampTypes = new[] { "OP", "ED" };
    private readonly ITimestampsService _timestampsService;

    public IMediaPlayer MediaPlayer { get; } = new WinUIMediaPlayerWrapper();

    public ICommand PlayRange { get; }


    private void Play()
    {
        MediaPlayer.Play(StartPosition);

        _subscription = this.WhenAnyValue(x => x.CurrentPlayerPosition)
            .Where(time => time >= EndPosition)
            .Subscribe(_ =>
            {
                MediaPlayer.Pause();
                _subscription?.Dispose();
            });
    }

    public async Task Submit()
    {
        await _timestampsService.SubmitTimeStamp(MalId, Episode, SelectedTimeStampType.ToLower(),  new Interval { StartTime = StartPosition, EndTime = EndPosition }, Duration);
    }

}
