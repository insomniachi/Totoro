using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Controls;

/// <summary>
/// The peer of a list's item that UI Automation (screen readers, winapp ui) can select. Toolkit lists with their own
/// panel (Segmented, TokenView) give their items only the plain item peer, with no selection: the items can be seen,
/// but not chosen. In a multiple-selection list the item also toggles, so it can be switched off again.
/// </summary>
public sealed partial class SelectableItemAutomationPeer(ListViewItem owner) : ListViewItemAutomationPeer(owner), ISelectionItemProvider, IToggleProvider
{
	private ListViewItem Item => (ListViewItem)Owner;

	private bool IsMultiple => ItemsControl.ItemsControlFromItemContainer(Item) is ListViewBase { SelectionMode: ListViewSelectionMode.Multiple };

	public bool IsSelected => Item.IsSelected;

	public IRawElementProviderSimple? SelectionContainer =>
		ItemsControl.ItemsControlFromItemContainer(Item) is { } list && FromElement(list) is { } peer ? ProviderFromPeer(peer) : null;

	public void Select() => Item.IsSelected = true;

	public void AddToSelection() => Select();

	// Single selection can't be left with none; multiple can.
	public void RemoveFromSelection()
	{
		if (IsMultiple)
		{
			Item.IsSelected = false;
		}
	}

	public ToggleState ToggleState => Item.IsSelected ? ToggleState.On : ToggleState.Off;

	public void Toggle() => Item.IsSelected = !Item.IsSelected;

	protected override object? GetPatternCore(PatternInterface patternInterface) => patternInterface switch
	{
		PatternInterface.SelectionItem => this,
		PatternInterface.Toggle when IsMultiple => this,
		_ => base.GetPatternCore(patternInterface),
	};
}
