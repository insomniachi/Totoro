using Microsoft.UI.Xaml;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>Where an anime is on the user's list (e.g. "Watching"), on its card's top left; none when it isn't on it.</summary>
public sealed class ListStatusOverlay : LiveAnimeCardOverlay<LabelBadge>
{
	protected override LabelBadge Create() => new(HorizontalAlignment.Left, VerticalAlignment.Top);

	protected override void Update(LabelBadge badge, AnimeModel? anime, DateTime now)
	{
		var status = anime?.Tracking?.Status;
		badge.Visibility = status is null ? Visibility.Collapsed : Visibility.Visible;
		if (status is { } value)
		{
			var text = value switch
			{
				ListItemStatus.Watching => "Watching",
				ListItemStatus.PlanToWatch => "Plan to watch",
				ListItemStatus.Completed => "Completed",
				ListItemStatus.OnHold => "On hold",
				ListItemStatus.Dropped => "Dropped",
				ListItemStatus.Rewatching => "Rewatching",
				_ => value.ToString(),
			};
			badge.Set("", text, $"On your list: {text}");
		}
	}
}
