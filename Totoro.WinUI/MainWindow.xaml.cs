using Microsoft.UI.Windowing;
using Totoro.WinUI.Contracts;
using WinUIEx;

namespace Totoro.WinUI;

public sealed partial class MainWindow : WindowEx
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;

		App.GetService<IWindowService>()
		   .IsFullWindowChanged
		   .ObserveOn(RxApp.MainThreadScheduler)
		   .Subscribe(isFullWindow =>
		   {
			   var presenterKind = isFullWindow ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Overlapped;
			   TitleBar.Visibility = isFullWindow ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
			   App.MainWindow.AppWindow.SetPresenter(presenterKind);
		   });
	}
}
