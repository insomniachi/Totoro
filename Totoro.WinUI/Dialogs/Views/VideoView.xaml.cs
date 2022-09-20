using AnimDL.WinUI.Dialogs.ViewModels;
using Windows.Media.Core;

namespace AnimDL.WinUI.Dialogs.Views;

public class VideoViewBase : ReactivePage<PlayVideoDialogViewModel> { }

public sealed partial class VideoView : VideoViewBase
{
    public VideoView()
    {
        InitializeComponent();

        this.WhenActivated(d =>
        {
            this.WhenAnyValue(x => x.ViewModel.Url)
                .WhereNotNull()
                .Select(x => MediaSource.CreateFromUri(new Uri(x)))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(x => MediaPlayerElement.Source = x);

            ViewModel
                .Close
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => MediaPlayerElement.Source = null);

        });
    }
}
