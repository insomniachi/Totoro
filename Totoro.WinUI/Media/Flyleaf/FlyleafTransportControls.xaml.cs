using FlyleafLib.MediaPlayer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReactiveMarbles.ObservableEvents;

namespace Totoro.WinUI.Media.Flyleaf;

public sealed partial class FlyleafTransportControls : UserControl, IMediaTransportControls
{
    public bool IsNextTrackButtonVisible
    {
        get { return (bool)GetValue(IsNextTrackButtonVisibleProperty); }
        set { SetValue(IsNextTrackButtonVisibleProperty, value); }
    }

    public bool IsPreviousTrackButtonVisible
    {
        get { return (bool)GetValue(IsPreviousTrackButtonVisibleProperty); }
        set { SetValue(IsPreviousTrackButtonVisibleProperty, value); }
    }

    public bool IsSkipButtonVisible
    {
        get { return (bool)GetValue(IsSkipButtonVisibleProperty); }
        set { SetValue(IsSkipButtonVisibleProperty, value); }
    }

    public bool IsAddCCButtonVisibile
    {
        get { return (bool)GetValue(IsAddCCButtonVisibileProperty); }
        set { SetValue(IsAddCCButtonVisibileProperty, value); }
    }

    public bool IsCCSelectionVisible
    {
        get { return (bool)GetValue(IsCCSelectionVisibleProperty); }
        set { SetValue(IsCCSelectionVisibleProperty, value); }
    }

    public static readonly DependencyProperty IsCCSelectionVisibleProperty =
        DependencyProperty.Register("IsCCSelectionVisible", typeof(bool), typeof(FlyleafTransportControls), new PropertyMetadata(false));

    public static readonly DependencyProperty IsAddCCButtonVisibileProperty =
        DependencyProperty.Register("IsAddCCButtonVisibile", typeof(bool), typeof(FlyleafTransportControls), new PropertyMetadata(false));

    public static readonly DependencyProperty IsSkipButtonVisibleProperty =
        DependencyProperty.Register("IsSkipButtonVisible", typeof(bool), typeof(FlyleafTransportControls), new PropertyMetadata(false));

    public static readonly DependencyProperty IsPreviousTrackButtonVisibleProperty =
        DependencyProperty.Register("IsPreviousTrackButtonVisible", typeof(bool), typeof(FlyleafTransportControls), new PropertyMetadata(false));

    public static readonly DependencyProperty IsNextTrackButtonVisibleProperty =
        DependencyProperty.Register("IsNextTrackButtonVisible", typeof(bool), typeof(FlyleafTransportControls), new PropertyMetadata(false));


    public Player Player
    {
        get { return (Player)GetValue(PlayerProperty); }
        set { SetValue(PlayerProperty, value); }
    }

    public IObservable<Unit> OnNextTrack { get; }
    public IObservable<Unit> OnPrevTrack { get; }
    public IObservable<Unit> OnStaticSkip { get; }
    public IObservable<Unit> OnDynamicSkip { get; }
    public IObservable<Unit> OnAddCc { get; }
    public IObservable<string> OnQualityChanged { get; }
    public IObservable<Unit> OnSubmitTimeStamp { get; }
    public string SelectedResolution { get; set; }
    public IEnumerable<string> Resolutions { get; set; }

    public IObservable<PlaybackRate> PlaybackRateChanged => Observable.Empty<PlaybackRate>();

    public static readonly DependencyProperty PlayerProperty =
        DependencyProperty.Register("Player", typeof(Player), typeof(FlyleafTransportControls), new PropertyMetadata(null));

    public FlyleafTransportControls()
    {
        this.InitializeComponent();

        OnNextTrack = NextTrackButton.Events().Click.Select(_ => Unit.Default);
        OnPrevTrack = PreviousTrackButton.Events().Click.Select(_ => Unit.Default);
        OnStaticSkip = SkipIntroButton.Events().Click.Select(_ => Unit.Default);
        OnDynamicSkip = Observable.Empty<Unit>();
        OnAddCc = Observable.Empty<Unit>();
        OnQualityChanged = Observable.Empty<string>();
        OnSubmitTimeStamp = SubmitTimeStampButton.Events().Click.Select(_ => Unit.Default);
    }

    private void SkipBackwardButton_Click(object sender, RoutedEventArgs e)
    {
        var ts = new TimeSpan(Player.CurTime) - TimeSpan.FromSeconds(10);
        Player.SeekAccurate((int)ts.TotalMilliseconds);
    }

    private void SkipForwardButton_Click(object sender, RoutedEventArgs e)
    {
        var ts = new TimeSpan(Player.CurTime) + TimeSpan.FromSeconds(30);
        Player.SeekAccurate((int)ts.TotalMilliseconds);
    }
}
