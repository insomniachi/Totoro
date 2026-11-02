using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>An overlay the card refreshes when its item changes or a minute passes (see <see cref="AnimeCard.Overlays"/>).</summary>
internal interface ILiveOverlay
{
	void Update(IMediaModel? item, DateTime now);
}

/// <summary>
/// An overlay always on its card that shows something about the card's anime that changes with time and with the
/// user's list (e.g. when its next episode airs). The card's <see cref="CardOverlaySelector"/> attaches it only while
/// it has something to show; the card then updates it when it shows another item (cards are reused for others as a
/// list scrolls), when the item's place on the list changes, and each minute (one timer for every card).
/// </summary>
public abstract class LiveAnimeCardOverlay<TControl> : AnimeCardOverlay<TControl>, ILiveOverlay
	where TControl : UIElement
{
	private TControl? _control;

	protected sealed override OverlayMode Mode => OverlayMode.Always;

	protected sealed override TControl CreateOverlay() => _control = Create();

	protected abstract TControl Create();

	/// <summary>
	/// Shows the card's anime or manga (null when it has none) at <paramref name="now"/>. By default the anime's:
	/// overlays for both override this one.
	/// </summary>
	protected virtual void Update(TControl control, IMediaModel? item, DateTime now) => Update(control, item as AnimeModel, now);

	/// <summary>Shows the anime (null when the card has none, or has a manga) at <paramref name="now"/>.</summary>
	protected virtual void Update(TControl control, AnimeModel? anime, DateTime now)
	{
	}

	// Filled in here, not by the card: Interaction re-attaches behaviors (recreating the control) after the card's Loaded.
	protected override void OnAttached()
	{
		base.OnAttached();
		((ILiveOverlay)this).Update(AssociatedObject.Media, DateTime.Now);
	}

	protected override void OnDetaching()
	{
		base.OnDetaching();
		_control = null;
	}

	void ILiveOverlay.Update(IMediaModel? item, DateTime now)
	{
		if (_control is not null)
		{
			Update(_control, item, now);
		}
	}
}

/// <summary>One timer for every live overlay of every card (see AnimeCard.Overlays), ticking each minute while any follows it.</summary>
internal static class MinuteTimer
{
	private static DispatcherQueueTimer? s_timer;
	private static Action? s_tick;

	public static event Action Tick
	{
		add
		{
			s_tick += value;
			if (s_timer is null && DispatcherQueue.GetForCurrentThread() is { } queue)
			{
				s_timer = queue.CreateTimer();
				s_timer.Interval = TimeSpan.FromMinutes(1);
				s_timer.Tick += (_, _) => s_tick?.Invoke();
				s_timer.Start();
			}
		}
		remove
		{
			s_tick -= value;
			if (s_tick is null && s_timer is not null)
			{
				s_timer.Stop();
				s_timer = null;
			}
		}
	}
}
