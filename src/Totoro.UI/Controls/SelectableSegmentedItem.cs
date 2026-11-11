using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml.Automation.Peers;

namespace Totoro.UI.Controls;

/// <summary>A <see cref="SegmentedItem"/> that UI Automation can select (see <see cref="SelectableItemAutomationPeer"/>). Use it in a <see cref="SelectableSegmented"/>.</summary>
public partial class SelectableSegmentedItem : SegmentedItem
{
	protected override AutomationPeer OnCreateAutomationPeer() => new SelectableItemAutomationPeer(this);
}
