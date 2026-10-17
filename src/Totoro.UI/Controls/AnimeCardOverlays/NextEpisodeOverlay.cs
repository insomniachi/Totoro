using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Totoro.Core;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;

namespace Totoro.UI.Controls.AnimeCardOverlays;

/// <summary>
/// The next episode of an anime being watched or planned, and how long until it airs (e.g. "Ep 7", "2d 4h"), on its card, while there's
/// one coming and the setting's on (see <see cref="CardPreferences"/>). It follows the card's anime (cards are reused for
/// others as a list scrolls) and its place on the list, and counts down with one timer shared by every card.
/// </summary>
public sealed class NextEpisodeOverlay : AnimeCardOverlay<NextEpisodeBadge>, IRecipient<CardPreferencesChangedMessage>
{
	private NextEpisodeBadge? _badge;
	private AnimeModel? _anime;
	private long _animeToken;

	protected override OverlayMode Mode => OverlayMode.Always;

	protected override NextEpisodeBadge CreateOverlay() => _badge = new NextEpisodeBadge();

	protected override void OnAttached()
	{
		base.OnAttached();
		_animeToken = AssociatedObject.RegisterPropertyChangedCallback(AnimeCard.AnimeProperty, (_, _) => Follow(AssociatedObject.Anime));
		WeakReferenceMessenger.Default.Register(this);
		MinuteTimer.Tick += Update;
		Follow(AssociatedObject.Anime);
	}

	protected override void OnDetaching()
	{
		AssociatedObject.UnregisterPropertyChangedCallback(AnimeCard.AnimeProperty, _animeToken);
		WeakReferenceMessenger.Default.Unregister<CardPreferencesChangedMessage>(this);
		MinuteTimer.Tick -= Update;
		Follow(null);
		base.OnDetaching();
		_badge = null;
	}

	public void Receive(CardPreferencesChangedMessage message) => Update();

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
		if (e.PropertyName is nameof(AnimeModel.Tracking) or nameof(AnimeModel.AiringStatus))
		{
			Update();
		}
	}

	private void Update()
	{
		if (_badge is null)
		{
			return;
		}

		var left = _anime?.NextEpisodeAt - DateTime.Now;
		var shown = IsEnabled() && _anime?.Tracking?.Status is ListItemStatus.Watching or ListItemStatus.PlanToWatch && left > TimeSpan.Zero;
		_badge.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
		if (shown && _anime is { NextEpisodeAt: { } at } anime)
		{
			// The one after those aired (both databases count them: AniList from its schedule, MyAnimeList from the weekly slot).
			var episode = anime.AiredEpisodes + 1;
			_badge.Set(episode, Format(left!.Value), $"Episode {episode} airs {at.ToString("ddd d MMM, HH:mm", CultureInfo.CurrentCulture)}");
		}
	}

	/// <summary>e.g. "2d 4h", "5h 20m", "40m".</summary>
	internal static string Format(TimeSpan left)
	{
		if (left.TotalDays >= 1)
		{
			return left.Hours > 0 ? $"{left.Days}d {left.Hours}h" : $"{left.Days}d";
		}

		if (left.TotalHours >= 1)
		{
			return left.Minutes > 0 ? $"{left.Hours}h {left.Minutes}m" : $"{left.Hours}h";
		}

		return $"{Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))}m";
	}

	private static bool IsEnabled()
	{
		try
		{
			return Locator.GetRequiredService<IPreferences>().ShowNextEpisodeOnCards;
		}
		catch (InvalidOperationException)
		{
			// No services (e.g. in the designer): shown.
			return true;
		}
	}

	/// <summary>One timer for every card's countdown, ticking each minute while any card follows it.</summary>
	private static class MinuteTimer
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
}
