using Totoro.WinUI.Dialogs.ViewModels;

namespace Totoro.WinUI.Dialogs.Views;

public class RequestRatingViewBase : ReactivePage<RequestRatingViewModel> { }

public sealed partial class RequestRatingView : RequestRatingViewBase
{
    public RequestRatingView()
    {
        InitializeComponent();
    }
}
