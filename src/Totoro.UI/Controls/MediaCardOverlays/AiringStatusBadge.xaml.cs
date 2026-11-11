using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Navigation;

namespace Totoro.UI.Controls.MediaCardOverlays;

/// <summary>The badge <see cref="AiringStatusOverlay"/> puts on a card.</summary>
public sealed partial class AiringStatusBadge : UserControl
{
	public AiringStatusBadge()
	{
		InitializeComponent();
	}

	/// <summary>A green dot while airing (or publishing), an amber one before (finished ones get no badge).</summary>
	public void Set(AiringStatus status, MediaMode kind)
	{
		var airing = status == AiringStatus.CurrentlyAiring;
		AiringDot.Visibility = airing ? Visibility.Visible : Visibility.Collapsed;
		UpcomingDot.Visibility = airing ? Visibility.Collapsed : Visibility.Visible;
		AutomationProperties.SetName(this, (airing, kind) switch
		{
			(true, MediaMode.Manga) => "Publishing",
			(false, MediaMode.Manga) => "Not yet published",
			(true, _) => "Currently airing",
			_ => "Not yet aired",
		});
	}
}
