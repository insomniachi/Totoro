using FlyleafLib.MediaPlayer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.WinUI.Media.Flyleaf;

public sealed partial class FlyleafTransportControls : UserControl, IMediaTransportControls
{

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
    public bool IsSkipButtonVisible { get; set; }
    public bool IsNextTrackButtonVisible { get; set; }
    public bool IsPreviousTrackButtonVisible { get; set; }
    public bool IsAddCCButtonVisibile { get; set; }
    public bool IsCCSelectionVisible { get; set; }
    public string SelectedResolution { get; set; }
    public IEnumerable<string> Resolutions { get; set; }

    public static readonly DependencyProperty PlayerProperty =
        DependencyProperty.Register("Player", typeof(Player), typeof(FlyleafTransportControls), new PropertyMetadata(null));

    public FlyleafTransportControls()
    {
        this.InitializeComponent();

        OnNextTrack = Observable.Empty<Unit>();
        OnPrevTrack = Observable.Empty<Unit>();
        OnStaticSkip = Observable.Empty<Unit>();
        OnDynamicSkip = Observable.Empty<Unit>();
        OnAddCc = Observable.Empty<Unit>();
        OnQualityChanged = Observable.Empty<string>();
        OnSubmitTimeStamp = Observable.Empty<Unit>();
    }
}
