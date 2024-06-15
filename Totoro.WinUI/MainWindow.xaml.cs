using Microsoft.UI.Xaml;
using WinUIEx;

namespace Totoro.WinUI;

public sealed partial class MainWindow : WindowEx
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;
        TitleBar.Loaded += TitleBar_Loaded;
    }

    private void TitleBar_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Parts get delay loaded. If you have the parts, make them visible.
        VisualStateManager.GoToState(TitleBar, "SubtitleTextVisible", true);
        VisualStateManager.GoToState(TitleBar, "HeaderVisible", true);
        VisualStateManager.GoToState(TitleBar, "ContentVisible", true);
        VisualStateManager.GoToState(TitleBar, "FooterVisible", true);

        // Run layout so we re-calculate the drag regions.
        TitleBar.InvalidateMeasure();
    }
}
