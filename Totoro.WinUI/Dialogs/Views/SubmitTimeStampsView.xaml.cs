using Totoro.WinUI.Dialogs.ViewModels;
using ReactiveMarbles.ObservableEvents;

namespace Totoro.WinUI.Dialogs.Views;

public class SubmitTimeStampsViewBase : ReactivePage<SubmitTimeStampsViewModel> { }

public sealed partial class SubmitTimeStampsView : SubmitTimeStampsViewBase
{
    public SubmitTimeStampsView()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            this.WhenAnyValue(x => x.ViewModel.MediaPlayer)
                .Do(wrapper => MediaPlayerElement.SetMediaPlayer(wrapper.GetMediaPlayer()))
                .Subscribe()
                .DisposeWith(d);

            SetStartButton
            .Events()
            .Click
            .Select(_ => Unit.Default)
            .InvokeCommand(ViewModel.SetStartPosition)
            .DisposeWith(d);

            SetEndButton
            .Events()
            .Click
            .Select(_ => Unit.Default)
            .InvokeCommand(ViewModel.SetStartPosition)
            .DisposeWith(d);
        });
    }
}
