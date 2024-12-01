using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
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

	private void TitleBar_PaneToggleRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
	{
		if(sender is null)
		{
			return;
		}

		Shell.AppTitleBar_PaneButtonClick(sender, new RoutedEventArgs());
    }

	private void TitleBar_BackRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
	{
		if (sender is null)
		{
			return;
		}

		Shell.AppTitleBar_BackButtonClick(sender, new RoutedEventArgs());
	}
}
