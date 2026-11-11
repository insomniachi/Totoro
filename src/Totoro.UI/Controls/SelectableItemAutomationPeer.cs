using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Controls;

/// <summary>
/// The peer of a single-selection list's item that UI Automation (screen readers, winapp ui) can select. Toolkit lists
/// with their own panel (Segmented, TokenView) give their items only the plain item peer, with no selection: the items
/// can be seen, but not chosen.
/// </summary>
public sealed partial class SelectableItemAutomationPeer(ListViewItem owner) : ListViewItemAutomationPeer(owner), ISelectionItemProvider
{
	public bool IsSelected => owner.IsSelected;

	public IRawElementProviderSimple? SelectionContainer =>
		ItemsControl.ItemsControlFromItemContainer(owner) is { } list && FromElement(list) is { } peer ? ProviderFromPeer(peer) : null;

	public void Select() => owner.IsSelected = true;

	// Single selection: adding an item selects it, and the list can't be left with none.
	public void AddToSelection() => Select();

	public void RemoveFromSelection()
	{
	}

	protected override object? GetPatternCore(PatternInterface patternInterface) =>
		patternInterface == PatternInterface.SelectionItem ? this : base.GetPatternCore(patternInterface);
}
