using System.Reactive.Subjects;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnimDL.WinUI.Media;

public class CustomMediaTransportControls : MediaTransportControls
{
    private readonly Subject<Unit> _onNextTrack = new();
    private readonly Subject<Unit> _onPrevTrack = new();
    private readonly Subject<Unit> _onSkipIntro = new();
    private readonly Subject<string> _onQualityChanged = new();
    private readonly Subject<Unit> _onDynamicSkipIntro = new();
    private readonly MenuFlyout _qualitiesFlyout = new() { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Top };
    private AppBarButton _qualitiesButton;
    private Button _dynamicSkipIntroButton;

    public static readonly DependencyProperty QualitiesProperty =
        DependencyProperty.Register("Qualities", typeof(IEnumerable<string>), typeof(CustomMediaTransportControls), new PropertyMetadata(null, OnQualitiesChanged));

    public static readonly DependencyProperty IsSkipIntroButtonVisibleProperty =
        DependencyProperty.Register("IsSkipIntroButtonVisible", typeof(bool), typeof(CustomMediaTransportControls), new PropertyMetadata(false, OnSkipIntroVisibleChanged));

    private static void OnSkipIntroVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var mtc = d as CustomMediaTransportControls;
        if (mtc._dynamicSkipIntroButton is Button btn && e.NewValue is bool b)
        {
            btn.Visibility = b ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnQualitiesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var mtc = d as CustomMediaTransportControls;
        var flyout = mtc._qualitiesFlyout;
        foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
        {
            item.Click -= mtc.FlyoutItem_Click;
        }
        flyout.Items.Clear();

        if (e.NewValue is IEnumerable<string> values)
        {
            var qualities = values.ToList();
            if (qualities.Count <= 1)
            {
                mtc._onQualityChanged.OnNext("default");
                return;
            }

            mtc._qualitiesButton.IsEnabled = true;
            foreach (var item in qualities)
            {
                var flyoutItem = new MenuFlyoutItem { Text = item };
                flyoutItem.Click += mtc.FlyoutItem_Click;
                flyout.Items.Add(flyoutItem);
            }
        }
    }

    public IEnumerable<string> Qualities
    {
        get { return (IEnumerable<string>)GetValue(QualitiesProperty); }
        set { SetValue(QualitiesProperty, value); }
    }

    public bool IsSkipIntroButtonVisible
    {
        get { return (bool)GetValue(IsSkipIntroButtonVisibleProperty); }
        set { SetValue(IsSkipIntroButtonVisibleProperty, value); }
    }

    public IObservable<Unit> OnNextTrack => _onNextTrack;
    public IObservable<Unit> OnPrevTrack => _onPrevTrack;
    public IObservable<Unit> OnSkipIntro => _onSkipIntro;
    public IObservable<string> OnQualityChanged => _onQualityChanged;
    public IObservable<Unit> OnDynamicSkipIntro => _onDynamicSkipIntro;

    public CustomMediaTransportControls()
    {
        DefaultStyleKey = typeof(CustomMediaTransportControls);
    }

    protected override void OnApplyTemplate()
    {
        var nextTrackButton = GetTemplateChild("NextTrackButton") as AppBarButton;
        var prevTrackButton = GetTemplateChild("PreviousTrackButton") as AppBarButton;
        var skipIntroButton = GetTemplateChild("SkipIntroButton") as AppBarButton;
        _qualitiesButton = GetTemplateChild("QualitiesButton") as AppBarButton;
        _qualitiesButton.Flyout = _qualitiesFlyout;
        _dynamicSkipIntroButton = GetTemplateChild("DynamicSkipIntroButton") as Button;


        prevTrackButton.Click += (_, __) => _onPrevTrack.OnNext(Unit.Default);
        nextTrackButton.Click += (_, __) => _onNextTrack.OnNext(Unit.Default);
        skipIntroButton.Click += (_, __) => _onSkipIntro.OnNext(Unit.Default);
        _dynamicSkipIntroButton.Click += (_, __) => _onDynamicSkipIntro.OnNext(Unit.Default);

        base.OnApplyTemplate();
    }

    private void FlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        _onQualityChanged.OnNext((sender as MenuFlyoutItem).Text);
    }
}
