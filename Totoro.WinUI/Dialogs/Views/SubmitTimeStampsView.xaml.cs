using Totoro.WinUI.Dialogs.ViewModels;
using Totoro.WinUI.Media;
using Windows.Media.Core;

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
                .Where(mediaPlayer => mediaPlayer is WinUIMediaPlayerWrapper)
                .Select(mediaPlayer => mediaPlayer as WinUIMediaPlayerWrapper)
                .Do(wrapper => MediaPlayerElement.SetMediaPlayer(wrapper.GetMediaPlayer()))
                .Subscribe()
                .DisposeWith(d);

            this.WhenAnyValue(x => x.ViewModel.MediaUrl)
                .Where(x => !string.IsNullOrEmpty(x))
                .Subscribe(url => MediaPlayerElement.Source = MediaSource.CreateFromUri(new Uri(url)))
                .DisposeWith(d);

        });
    }
}
