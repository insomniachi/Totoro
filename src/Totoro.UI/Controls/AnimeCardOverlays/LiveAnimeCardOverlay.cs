using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Totoro.Core.Modules.Anime;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// An overlay always on its card that shows something about the card's anime that changes with time and with the
/// user's list (e.g. when its next episode airs): it's updated when the card shows another anime (cards are reused for
/// others as a list scrolls), when the anime's place on the list changes, and each minute (one timer for every card).
/// </summary>
public abstract class LiveAnimeCardOverlay<TControl> : AnimeCardOverlay<TControl>
	where TControl : UIElement
{
	private TControl? _control;
	private AnimeModel? _anime;
	private long _animeToken;

	protected sealed override OverlayMode Mode => OverlayMode.Always;

	protected sealed override TControl CreateOverlay() => _control = Create();

	protected abstract TControl Create();

	/// <summary>Shows what there is to show for the anime (null when the card has none) at <paramref name="now"/>.</summary>
	protected abstract void Update(TControl control, AnimeModel? anime, DateTime now);

	protected override void OnAttached()
	{
		base.OnAttached();
		_animeToken = AssociatedObject.RegisterPropertyChangedCallback(AnimeCard.AnimeProperty, (_, _) => Follow(AssociatedObject.Anime));
		MinuteTimer.Tick += Update;
		Follow(AssociatedObject.Anime);
	}

	protected override void OnDetaching()
	{
		AssociatedObject.UnregisterPropertyChangedCallback(AnimeCard.AnimeProperty, _animeToken);
		MinuteTimer.Tick -= Update;
		Follow(null);
		base.OnDetaching();
		_control = null;
	}

	private void Follow(AnimeModel? anime)
	{
		if (_anime is not null)
		{
			_anime.PropertyChanged -= OnAnimeChanged;
		}

		_anime = anime;
		if (_anime is not null)
		{
			_anime.PropertyChanged += OnAnimeChanged;
		}

		Update();
	}

	private void OnAnimeChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(AnimeModel.Tracking) or nameof(AnimeModel.AiringStatus) or nameof(AnimeModel.ProviderEpisodes))
		{
			Update();
		}
	}

	private void Update()
	{
		if (_control is not null)
		{
			Update(_control, _anime, DateTime.Now);
		}
	}
}

/// <summary>One timer for every live overlay of every card (see LiveAnimeCardOverlay), ticking each minute while any follows it.</summary>
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
