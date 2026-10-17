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

	public void Set(int count)
	{
		Badge.Value = count;
		AutomationProperties.SetName(this, count == 1 ? "1 new episode to watch" : $"{count} new episodes to watch");
	}
}
