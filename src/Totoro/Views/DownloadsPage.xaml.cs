using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

public sealed partial class DownloadsPage : Page
{
	public DownloadsPage()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial DownloadsViewModel? ViewModel { get; set; }

	public static Visibility VisibleIfNone(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;

	public static bool IsUnknown(double progress) => progress < 0;

	public static double Percent(double progress) => Math.Max(0, progress) * 100;
}
