using System.Windows.Input;
using CommunityToolkit.WinUI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls;

public sealed partial class AnimeCard : UserControl
{
	public AnimeCard()
	{
		InitializeComponent();
		Tapped += OnTapped;
	}

	[GeneratedDependencyProperty]
	public partial AnimeModel? Anime { get; set; }

	/// <summary>Runs with the card's <see cref="Anime"/> when the card is clicked, e.g. to show its details.</summary>
	[GeneratedDependencyProperty]
	public partial ICommand? Command { get; set; }

	partial void OnCommandChanged(ICommand? newValue)
	{
		ProtectedCursor = newValue is null ? null : InputSystemCursor.Create(InputSystemCursorShape.Hand);
	}

	private void OnTapped(object sender, TappedRoutedEventArgs e)
	{
		if (IsFromOverlay(e.OriginalSource) || Command is not { } command || !command.CanExecute(Anime))
		{
			return;
		}

		command.Execute(Anime);
		e.Handled = true;
	}

	/// <summary>
	/// Whether a tap came from an overlay on the card (e.g. the actions button) rather than the card itself.
	/// Buttons don't mark Tapped handled, so their taps bubble up here too and would otherwise also open the card.
	/// Overlays are hosted in OverlayLayer, which itself covers the whole card, so a tap on the layer itself is a card tap.
	/// </summary>
	private bool IsFromOverlay(object? source)
	{
		for (var element = source as DependencyObject; element is not null && element != this; element = VisualTreeHelper.GetParent(element))
		{
			if (VisualTreeHelper.GetParent(element) == OverlayLayer)
			{
				return true;
			}
		}

		return false;
	}
}
