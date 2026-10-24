using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>The badge <see cref="NewEpisodesOverlay"/> puts on a card.</summary>
public sealed partial class NewEpisodesBadge : UserControl
{
	public NewEpisodesBadge()
	{
		InitializeComponent();
	}

	/// <param name="onProvider">Whether the provider has them; if not, they've aired but can't be watched yet (grey).</param>
	public void Set(int count, bool onProvider)
	{
		Badge.Value = count;
		// Null is InfoBadge's own (accent) style.
		Badge.Style = onProvider ? null : (Style)Application.Current.Resources["InformationalValueInfoBadgeStyle"];
		var episodes = count == 1 ? "1 new episode" : $"{count} new episodes";
		AutomationProperties.SetName(this, onProvider ? $"{episodes} to watch" : $"{episodes} aired, not on the provider yet");
	}
}
