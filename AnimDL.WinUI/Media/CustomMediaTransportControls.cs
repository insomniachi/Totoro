using System.Reactive.Subjects;
using Microsoft.UI.Xaml.Controls;

namespace AnimDL.WinUI.Media;

public class CustomMediaTransportControls : MediaTransportControls
{
    private readonly Subject<Unit> _onNextTrack = new();
    private readonly Subject<Unit> _onPrevTrack = new();
    private readonly Subject<Unit> _onSkipIntro = new();

    public IObservable<Unit> OnNextTrack => _onNextTrack;
    public IObservable<Unit> OnPrevTrack => _onPrevTrack;
    public IObservable<Unit> OnSkipIntro => _onSkipIntro;

    public CustomMediaTransportControls()
    {
        DefaultStyleKey = typeof(CustomMediaTransportControls);
    }

    protected override void OnApplyTemplate()
    {
        var nextTrackButton = GetTemplateChild("NextTrackButton") as AppBarButton;
        var prevTrackButton = GetTemplateChild("PreviousTrackButton") as AppBarButton;
        var skipIntroButton = GetTemplateChild("SkipIntroButton") as AppBarButton;


        prevTrackButton.Click += (_, __) => _onPrevTrack.OnNext(Unit.Default);
        nextTrackButton.Click += (_, __) => _onNextTrack.OnNext(Unit.Default);
        skipIntroButton.Click += (_, __) => _onSkipIntro.OnNext(Unit.Default);

        base.OnApplyTemplate();
    }
}
