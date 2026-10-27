using System.Windows.Input;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// An action a card overlay offers, e.g. one of the <see cref="ActionsOverlay"/>'s buttons or menu items.
/// <see cref="Command"/> receives <see cref="CommandParameter"/> if set, otherwise the card's <c>AnimeModel</c>.
/// </summary>
public sealed partial class CardAction : DependencyObject
{
	/// <summary>
	/// Any icon source: symbol, font glyph, path, bitmap or image. The Community Toolkit markup extensions keep it short,
	/// e.g. <c>Icon="{ui:SymbolIconSource Symbol=Play}"</c> or <c>Icon="{ui:FontIconSource Glyph=&amp;#xE768;}"</c>
	/// with <c>xmlns:ui="using:CommunityToolkit.WinUI"</c>.
	/// </summary>
	[GeneratedDependencyProperty]
	public partial IconSource? Icon { get; set; }

	/// <summary>The menu item's text, and the button's tooltip and name for screen readers.</summary>
	[GeneratedDependencyProperty]
	public partial string? Label { get; set; }

	/// <summary>
	/// What it's called instead while the card's anime isn't on the user's list, e.g. "Add to list" for "Edit tracking";
	/// null to keep <see cref="Label"/>.
	/// </summary>
	[GeneratedDependencyProperty]
	public partial string? OffListLabel { get; set; }

	/// <summary>Its icon while the card's anime isn't on the user's list; null to keep <see cref="Icon"/>.</summary>
	[GeneratedDependencyProperty]
	public partial IconSource? OffListIcon { get; set; }

	/// <summary>Also a button on the card while it's hovered, for actions used often; every action is in the card's menu.</summary>
	[GeneratedDependencyProperty]
	public partial bool ShowOnHover { get; set; }

	[GeneratedDependencyProperty]
	public partial ICommand? Command { get; set; }

	[GeneratedDependencyProperty]
	public partial object? CommandParameter { get; set; }
}

/// <summary>A list of <see cref="CardAction"/>s, so a set can be declared once as a resource and shared by every card.</summary>
public sealed class CardActionCollection : List<CardAction>;
