using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Totoro
{
	/// <summary>
	/// An empty window that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			ExtendsContentIntoTitleBar = true;
			AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;
		}

		public MainWindowViewModel? ViewModel { get; set; }
	}
}
