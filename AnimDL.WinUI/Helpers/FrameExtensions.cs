using Microsoft.UI.Xaml.Controls;
using ReactiveUI;

namespace AnimDL.WinUI.Helpers;

public static class FrameExtensions
{
    public static object GetPageViewModel(this Frame frame)
    {
        if (frame.Content is IViewFor view)
        {
            return view.ViewModel;
        }

        return null;
    }
}
