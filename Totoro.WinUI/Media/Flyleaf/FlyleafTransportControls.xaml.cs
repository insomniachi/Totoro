using System.Reactive.Subjects;
using FlyleafLib.MediaFramework.MediaStream;
using FlyleafLib.MediaPlayer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReactiveMarbles.ObservableEvents;
using Splat;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Media.Flyleaf;

public sealed partial class FlyleafTransportControls : UserControl, IMediaTransportControls, IEnableLogger
{
    private readonly Subject<string> _onQualityChanged = new();
    private readonly Subject<PlaybackRate> _onPlaybackRateChanged = new();
    private readonly Subject<bool> _onPiPModelToggle = new();
    private bool _isInPiPMode;

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

    public string SelectedResolution
    {
        get { return (string)GetValue(SelectedResolutionProperty); }
        set { SetValue(SelectedResolutionProperty, value); }
    }

    public Player Player
    {
        get { return (Player)GetValue(PlayerProperty); }
        set { SetValue(PlayerProperty, value); }
    }

    public IEnumerable<string> Resolutions
    {
        get { return (IEnumerable<string>)GetValue(ResolutionsProperty); }
        set { SetValue(ResolutionsProperty, value); }
    }

    public static readonly DependencyProperty ResolutionsProperty =
        DependencyProperty.Register("Resolutions", typeof(IEnumerable<string>), typeof(FlyleafTransportControls), new PropertyMetadata(null, OnResolutionsChanged));

    public static readonly DependencyProperty SelectedResolutionProperty =
        DependencyProperty.Register("SelectedResolution", typeof(string), typeof(FlyleafTransportControls), new PropertyMetadata("", OnSelectedResolutionChanged));

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

    public static readonly DependencyProperty PlayerProperty =
    DependencyProperty.Register("Player", typeof(Player), typeof(FlyleafTransportControls), new PropertyMetadata(null));

    private static void OnSelectedResolutionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not string s)
        {
            return;
        }

        if (string.IsNullOrEmpty(s))
        {
            return;
        }

        var mtc = d as FlyleafTransportControls;
        var flyout = mtc.QualitiesButton.Flyout as MenuFlyout;
        foreach (var item in flyout.Items.OfType<ToggleMenuFlyoutItem>())
        {
            item.IsChecked = item.Text == s;
        }
    }

    private static void OnResolutionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var mtc = d as FlyleafTransportControls;
        var flyout = mtc.QualitiesButton.Flyout as MenuFlyout;

        foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
        {
            item.Click -= mtc.FlyoutItem_Click;
        }

        flyout.Items.Clear();

        if (e.NewValue is IEnumerable<string> values)
        {
            var qualities = values.ToList();
            if (qualities.Count == 1)
            {
                mtc.QualitiesButton.Visibility = Visibility.Collapsed;
            }
            else if (qualities.Count > 1)
            {
                mtc.QualitiesButton.IsEnabled = true;
                foreach (var item in qualities)
                {
                    var flyoutItem = new ToggleMenuFlyoutItem { Text = item };
                    flyoutItem.Click += mtc.FlyoutItem_Click;
                    flyout.Items.Add(flyoutItem);
                }
            }
        }
    }

    private void FlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        _onQualityChanged.OnNext((sender as MenuFlyoutItem).Text);
    }

    public IObservable<Unit> OnNextTrack { get; }
    public IObservable<Unit> OnPrevTrack { get; }
    public IObservable<Unit> OnStaticSkip { get; }
    public IObservable<Unit> OnDynamicSkip { get; }
    public IObservable<Unit> OnAddCc { get; }
    public IObservable<string> OnQualityChanged { get; }
    public IObservable<Unit> OnSubmitTimeStamp { get; }
    public IObservable<PlaybackRate> PlaybackRateChanged { get; }
    public IObservable<bool> OnPiPModeToggle { get; }

    public FlyleafTransportControls()
    {
        InitializeComponent();

        var windowService = App.GetService<IWindowService>();

        OnNextTrack = NextTrackButton.Events().Click.Select(_ => Unit.Default);
        OnPrevTrack = PreviousTrackButton.Events().Click.Select(_ => Unit.Default);
        OnStaticSkip = SkipIntroButton.Events().Click.Select(_ => Unit.Default);
        OnDynamicSkip = DynamicSkipIntroButton.Events().Click.Select(_ => Unit.Default);
        OnAddCc = AddCcButton.Events().Click.Select(_ => Unit.Default);
        OnQualityChanged = _onQualityChanged;
        PlaybackRateChanged = _onPlaybackRateChanged;
        OnPiPModeToggle = _onPiPModelToggle;
        OnSubmitTimeStamp = SubmitTimeStampButton.Events().Click.Select(_ => Unit.Default);

        FullWindowButton
            .Events()
            .Click
            .Subscribe(_ => windowService.ToggleIsFullWindow());

        TimeSlider
            .Events()
            .ValueChanged
            .Where(x => x.NewValue > Converters.TiksToSeconds(Player.CurTime))
            .Subscribe(x => Player.SeekAccurate((int)TimeSpan.FromSeconds(x.NewValue).TotalMilliseconds));

        this.WhenAnyValue(x => x.Player.Status)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(status =>
            {
                if (status == Status.Playing)
                {
                    PlayPauseSymbol.Symbol = Symbol.Pause;
                }
                else if (status == Status.Paused)
                {
                    PlayPauseSymbol.Symbol = Symbol.Play;
                }
            });

        windowService
           .IsFullWindowChanged
           .Where(_ => FullWindowSymbol is not null)
           .Subscribe(isFullWindwow =>
           {
               FullWindowSymbol.Symbol = isFullWindwow ? Symbol.BackToWindow : Symbol.FullScreen;
           });
    }

    public string TimeRemaning(long currentTime, long duration)
    {
        var remaning = duration - currentTime;
        return new TimeSpan(remaning).ToString("hh\\:mm\\:ss");
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

    private void ToggleMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        var rate = (PlaybackRate)((ToggleMenuFlyoutItem)sender).Tag;
        _onPlaybackRateChanged.OnNext(rate);
    }

    public void UpdateSubtitleFlyout(ObservableCollection<SubtitlesStream> streams)
    {
        var flyout = CCSelectionButton.Flyout as MenuFlyout;
        flyout.Items.Clear();
        for (int i = 0; i < streams.Count; i++)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = streams[i].Title,
                IsChecked = i == 0,
            };
            item.Click += Item_Click;

            flyout.Items.Add(item);
        }
    }

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        var title = ((ToggleMenuFlyoutItem)sender).Text;
        var stream = Player.Subtitles.Streams.FirstOrDefault(x => x.Title == title);

        if(stream is null)
        {
            return;
        }

        var flyout = CCSelectionButton.Flyout as MenuFlyout;
        foreach (var item in flyout.Items.OfType<ToggleMenuFlyoutItem>())
        {
            if(item.Text != title)
            {
                item.IsChecked = false;
            }
        }

        Player.OpenAsync(stream);
    }

    private void PiPButton_Click(object sender, RoutedEventArgs e)
    {
        TogglePiPMode();
    }

    public void TogglePiPMode()
    {
        _isInPiPMode ^= true;
        PiPButton.Visibility = _isInPiPMode ? Visibility.Collapsed : Visibility.Visible;
        FullWindowButton.Visibility = _isInPiPMode ? Visibility.Collapsed : Visibility.Visible;
        _onPiPModelToggle.OnNext(_isInPiPMode);
    }
}
