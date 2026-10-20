using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>An icon and a short label on a card's corner, e.g. its list status (<see cref="ListStatusOverlay"/>).</summary>
public sealed partial class LabelBadge : UserControl
{
	public LabelBadge(HorizontalAlignment horizontal, VerticalAlignment vertical)
	{
		InitializeComponent();
		HorizontalAlignment = horizontal;
		VerticalAlignment = vertical;
	}

	/// <param name="description">What it says, in words, for screen readers: e.g. "Mean score 8.4 out of 10".</param>
	public void Set(string glyph, string text, string description)
	{
		Icon.Glyph = glyph;
		Label.Text = text;
		AutomationProperties.SetName(this, description);
	}
}
