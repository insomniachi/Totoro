using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>The badge <see cref="NextEpisodeOverlay"/> puts on a card.</summary>
public sealed partial class NextEpisodeBadge : UserControl
{
	public NextEpisodeBadge()
	{
		InitializeComponent();
	}

	/// <param name="episode">The episode's number.</param>
	/// <param name="time">How long until it airs, e.g. "2d 4h".</param>
	/// <param name="description">What it says, in words, for screen readers: e.g. "Episode 7 airs Sat 17 Oct, 22:30".</param>
	public void Set(int episode, string time, string description)
	{
		EpisodeText.Text = $"Ep {episode}";
		TimeText.Text = time;
		AutomationProperties.SetName(this, description);
	}
}
