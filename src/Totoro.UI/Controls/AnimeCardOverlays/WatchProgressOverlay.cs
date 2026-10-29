using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Navigation;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// How many episodes of an anime the user is watching they've watched (or chapters of a manga read), out of its total (e.g. "5/12", or "5/?" while the
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

	protected override void Update(LabelBadge badge, IMediaModel? item, DateTime now)
	{
		var shown = item?.ListStatus is ListItemStatus.Watching or ListItemStatus.Rewatching;
		badge.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
		if (shown)
		{
			var done = item!.Progress ?? 0;
			var total = item.Total is > 0 ? item.Total.ToString() : "?";
			badge.Set("", $"{done}/{total}", item.Kind == MediaMode.Manga ? $"Read {done} of {total} chapters" : $"Watched {done} of {total} episodes");
		}
	}
}
