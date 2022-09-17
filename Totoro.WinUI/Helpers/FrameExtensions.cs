using Microsoft.UI.Xaml.Controls;

namespace Totoro.WinUI.Helpers;

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
