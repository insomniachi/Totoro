using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;

namespace Totoro.UI.Controls;

/// <summary>A <see cref="Segmented"/> whose segments UI Automation can select (see <see cref="SelectableSegmentedItem"/>).</summary>
public partial class SelectableSegmented : Segmented
{
	// For ItemsSource; segments written in XAML are SelectableSegmentedItems themselves.
	protected override DependencyObject GetContainerForItemOverride() => new SelectableSegmentedItem();
}
