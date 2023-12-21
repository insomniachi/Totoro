using System.Reactive.Subjects;
using FlyleafLib.MediaPlayer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReactiveMarbles.ObservableEvents;
using Splat;

namespace Totoro.WinUI.Media.Flyleaf;

public sealed partial class FlyleafTransportControls : UserControl, IMediaTransportControls, IEnableLogger
{
    private readonly Subject<string> _onQualityChanged = new();

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
    public IObservable<PlaybackRate> PlaybackRateChanged => Observable.Empty<PlaybackRate>();

    public FlyleafTransportControls()
    {
        InitializeComponent();

        OnNextTrack = NextTrackButton.Events().Click.Select(_ => Unit.Default);
        OnPrevTrack = PreviousTrackButton.Events().Click.Select(_ => Unit.Default);
        OnStaticSkip = SkipIntroButton.Events().Click.Select(_ => Unit.Default);
        OnDynamicSkip = DynamicSkipIntroButton.Events().Click.Select(_ => Unit.Default);
        OnAddCc = AddCcButton.Events().Click.Select(_ => Unit.Default);
        OnQualityChanged = _onQualityChanged;
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

    public void Show()
    {
        bar.Visibility = Visibility.Visible;
        Observable.Timer(TimeSpan.FromSeconds(3))
                  .ObserveOn(RxApp.MainThreadScheduler)
                  .Subscribe(_ => bar.Visibility = Visibility.Collapsed);
    }
}
