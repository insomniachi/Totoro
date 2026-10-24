using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// How many episodes of an anime the user is watching they've watched, out of its total (e.g. "5/12", or "5/?" while the
/// total isn't known), on its card's bottom right; hidden while the card is hovered, where its actions show.
/// </summary>
public sealed class WatchProgressOverlay : LiveAnimeCardOverlay<LabelBadge>
{
	private LabelBadge? _badge;
	private PointerEventHandler? _onCaptureLost;

	protected override LabelBadge Create() => _badge = new(HorizontalAlignment.Right, VerticalAlignment.Bottom);

	protected override void OnAttached()
	{
		base.OnAttached();
		if (Layer is { } layer)
		{
			layer.PointerEntered += OnPointerEntered;
			layer.PointerExited += OnPointerExited;
			HoverPointer.AddCaptureLostHandler(layer, _onCaptureLost = OnPointerCaptureLost);
		}
	}

	protected override void OnDetaching()
	{
		if (Layer is { } layer)
		{
			layer.PointerEntered -= OnPointerEntered;
			layer.PointerExited -= OnPointerExited;
			if (_onCaptureLost is not null)
			{
				HoverPointer.RemoveCaptureLostHandler(layer, _onCaptureLost);
			}
		}

		_onCaptureLost = null;
		base.OnDetaching();
		_badge = null;
	}

	private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
	{
		if (HoverPointer.IsOutside(sender, e))
		{
			OnPointerExited(sender, e);
		}
	}

	// Opacity, not Visibility: Update owns Visibility.
	private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
	{
		if (_badge is not null)
		{
			_badge.Opacity = 0;
		}
	}

	private void OnPointerExited(object sender, PointerRoutedEventArgs e)
	{
		if (_badge is not null)
		{
			_badge.Opacity = 1;
		}
	}

	protected override void Update(LabelBadge badge, AnimeModel? anime, DateTime now)
	{
		var shown = anime?.Tracking?.Status is ListItemStatus.Watching or ListItemStatus.Rewatching;
		badge.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
		if (shown)
		{
			var watched = anime!.Tracking!.WatchedEpisodes ?? 0;
			var total = anime.TotalEpisodes is > 0 ? anime.TotalEpisodes.ToString() : "?";
			badge.Set("", $"{watched}/{total}", $"Watched {watched} of {total} episodes");
		}
	}
}
