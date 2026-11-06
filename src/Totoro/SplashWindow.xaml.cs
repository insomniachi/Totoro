using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinUIEx;

namespace Totoro;

/// <summary>Shows while the app does its startup work (see App.RunStartupAsync), before the main window opens.</summary>
public sealed partial class SplashWindow : Window
{
	public SplashWindow()
	{
		InitializeComponent();
		AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Totoro.ico"));
		if (AppWindow.Presenter is OverlappedPresenter presenter)
		{
			presenter.SetBorderAndTitleBar(true, false);
			presenter.IsResizable = false;
			presenter.IsMaximizable = false;
			presenter.IsMinimizable = false;
		}

		this.CenterOnScreen(360, 300);
	}

	/// <summary>What startup is doing, under the progress ring.</summary>
	public string Status
	{
		get => StatusText.Text;
		set => StatusText.Text = value;
	}
}
