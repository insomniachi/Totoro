using Microsoft.UI.Xaml;
using Totoro.Core.Modules;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>Where an anime or manga is on the user's list (e.g. "Watching"), on its card's top left; none when it isn't on it.</summary>
public sealed class ListStatusOverlay : LiveAnimeCardOverlay<LabelBadge>
{
	protected override LabelBadge Create() => new(HorizontalAlignment.Left, VerticalAlignment.Top);

	protected override void Update(LabelBadge badge, IMediaModel? item, DateTime now)
	{
		var status = item?.ListStatus;
		badge.Visibility = status is null ? Visibility.Collapsed : Visibility.Visible;
		if (status is not null)
		{
			// "Watching" for anime, "Reading" for manga.
			var text = MediaText.Status(status, item!.Kind);
			badge.Set("", text, $"On your list: {text}");
		}
	}
}
