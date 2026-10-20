using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Totoro.ViewModels;

namespace Totoro.Views;

/// <summary>Says why a page of the user's list is empty, and offers to sign in or retry (see <see cref="ListLoadState"/>).</summary>
public sealed partial class ListLoadInfoBars : UserControl
{
	public ListLoadInfoBars()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial ListLoadState? State { get; set; }

	public static bool HasText(string text) => text.Length > 0;
}
