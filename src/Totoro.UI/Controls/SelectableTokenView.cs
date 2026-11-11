using System.Runtime.CompilerServices;
using CommunityToolkit.Labs.WinUI;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Controls;

/// <summary>
/// A <see cref="TokenView"/> (pills) that UI Automation (screen readers, winapp ui) can read and select. With its default
/// peer, reading the scroller and wrap panel its pills are in hangs, and leaves the app's whole automation tree
/// unreadable once it has been shown.
/// </summary>
public partial class SelectableTokenView : TokenView
{
	protected override AutomationPeer OnCreateAutomationPeer() => new SelectableTokenViewAutomationPeer(this);

	// A list whose children are its pills directly. They keep their own type (TokenItem), which their style needs, and
	// get their selectable peer here: one per pill, so UI Automation sees the same element each time.
	private sealed partial class SelectableTokenViewAutomationPeer(SelectableTokenView owner) : FrameworkElementAutomationPeer(owner)
	{
		private readonly ConditionalWeakTable<ListViewItem, SelectableItemAutomationPeer> _peers = [];

		protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

		protected override string GetClassNameCore() => nameof(TokenView);

		protected override IList<AutomationPeer> GetChildrenCore() =>
		[
			.. Enumerable.Range(0, owner.Items.Count)
				.Select(owner.ContainerFromIndex)
				.OfType<ListViewItem>()
				.Select(item => _peers.GetValue(item, x => new SelectableItemAutomationPeer(x))),
		];
	}
}
