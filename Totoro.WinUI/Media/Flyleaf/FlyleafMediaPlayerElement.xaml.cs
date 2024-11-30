using FlyleafLib.MediaPlayer;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using Totoro.WinUI.Contracts;


namespace Totoro.WinUI.Media.Flyleaf;

public sealed partial class FlyleafMediaPlayerElement : UserControl
{
    private readonly Subject<Unit> _pointerMoved = new();
    private readonly IWindowService _windowService = App.GetService<IWindowService>();

    public Player Player
    {
        get { return (Player)GetValue(PlayerProperty); }
        set { SetValue(PlayerProperty, value); }
    }

    public static readonly DependencyProperty PlayerProperty =
        DependencyProperty.Register("Player", typeof(Player), typeof(FlyleafMediaPlayerElement), new PropertyMetadata(null));

    public FlyleafMediaPlayerElement()
    {
        InitializeComponent();

        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);

        _pointerMoved
            .Throttle(TimeSpan.FromSeconds(3))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ =>
            {
                TransportControls.Bar.Visibility = Visibility.Collapsed;

                if(_windowService.IsFullWindow)
                {
                    ProtectedCursor.Dispose();
                }

            }, RxApp.DefaultExceptionHandler.OnError);

        this.WhenAnyValue(x => x.Player.Status)
            .Where(status => status is Status.Playing)
            .Subscribe(_ => ShowTransportControls());

        TransportControls.DoubleTapped += (sender, args) => args.Handled = true;
    }

    private void FSC_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        ShowTransportControls();
    }

    private void ShowTransportControls()
    {
        RxApp.MainThreadScheduler.Schedule(() =>
        {
            TransportControls.Bar.Visibility = Visibility.Visible;
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow); 
        });

        _pointerMoved.OnNext(Unit.Default);
    }
}
