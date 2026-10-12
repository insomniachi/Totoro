using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Services;

namespace Totoro.UI.Controls;

/// <summary>A song's card, which plays it: in the Music tab, and in the chat. See <see cref="SongItem"/>.</summary>
public sealed partial class SongCard : UserControl
{
	public SongCard()
	{
		InitializeComponent();
	}

	[GeneratedDependencyProperty]
	public partial SongItem? Song { get; set; }

	public static Visibility VisibleIfAny(string text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
}
