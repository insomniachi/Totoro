using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>The badge <see cref="AiringStatusOverlay"/> puts on a card.</summary>
public sealed partial class AiringStatusBadge : UserControl
{
	public AiringStatusBadge()
	{
		InitializeComponent();
	}

	/// <summary>A green dot while airing, an amber one before (finished anime get no badge).</summary>
	public void Set(AiringStatus status)
	{
		var airing = status == AiringStatus.CurrentlyAiring;
		AiringDot.Visibility = airing ? Visibility.Visible : Visibility.Collapsed;
		UpcomingDot.Visibility = airing ? Visibility.Collapsed : Visibility.Visible;
		AutomationProperties.SetName(this, airing ? "Currently airing" : "Not yet aired");
	}
}
