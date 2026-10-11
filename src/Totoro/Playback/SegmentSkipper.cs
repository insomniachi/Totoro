using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;

namespace Totoro.Playback;

/// <summary>
/// An episode's opening, ending and recap, for one playback: marks them as chapters in the player (so they show on
/// its seek bar, and its next-chapter key skips them), then skips or offers to skip each as playback enters it,
/// following the given modes (the anime's own, or the app-wide ones). Each segment is skipped once, so seeking back
/// into one doesn't skip it again. Offering to skip is a button over the video in the built-in player (as playback
/// enters the segment, for a few seconds), and a hint to press the next-chapter key in others (once).
/// </summary>
internal sealed class SegmentSkipper(IMediaPlayer player, SkipMode openingMode, SkipMode endingMode, ILogger logger)
{
	// How long the "press Page Up to skip" hint stays up, at most.
	private static readonly TimeSpan s_hintDuration = TimeSpan.FromSeconds(8);

	private readonly Lock _lock = new();
	private readonly HashSet<MediaSegment> _handled = [];
	private IReadOnlyList<MediaSegment> _segments = [];

	// The built-in player's skip button: the segment it's for, and what takes it away.
	private MediaSegment? _offered;
	private CancellationTokenSource? _offer;

	/// <summary>Gets the episode's segments (they're matched to its length) and marks them in the player.</summary>
	public async Task LoadAsync(IMediaSegmentsProvider? provider, AnimeModel anime, int episode, TimeSpan duration, CancellationToken ct)
	{
		if (provider is null || anime.ExternalIds.MyAnimeList <= 0)
		{
			return;
		}

		try
		{
			List<MediaSegment> segments =
			[
				.. (await provider.GetSegmentsAsync(anime.ExternalIds.MyAnimeList, episode, duration, ct))
				   .Where(x => x.End > x.Start && x.Start < duration)
			];
			if (segments.Count == 0)
			{
				return;
			}

			lock (_lock)
			{
				_segments = segments;
			}

			await player.SetChaptersAsync(ToChapters(segments, duration));
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Couldn't mark the opening and ending");
		}
	}

	public void OnPosition(TimeSpan position)
	{
		// In it and not nearly over.
		bool IsIn(MediaSegment x) => position >= x.Start && position < x.End - TimeSpan.FromSeconds(1);

		List<MediaSegment> entered;
		MediaSegment? current;
		lock (_lock)
		{
			// Once each.
			entered = [.. _segments.Where(x => IsIn(x) && _handled.Add(x))];
			current = _segments.FirstOrDefault(IsIn);
		}

		if (player is IInteractiveMediaPlayer interactive)
		{
			// Offered with a button instead.
			UpdateOffer(interactive, current);
			entered.RemoveAll(x => ModeOf(x) == SkipMode.Ask);
		}

		foreach (var segment in entered)
		{
			_ = HandleAsync(segment, position);
		}
	}

	/// <summary>Takes away the skip button, when playback ends.</summary>
	public void Stop() => UpdateOffer(null, null);

	/// <summary>Shows the skip button for the segment playback is in, if it's offered; takes it away once out of it.</summary>
	private void UpdateOffer(IInteractiveMediaPlayer? interactive, MediaSegment? current)
	{
		if (current == _offered)
		{
			return;
		}

		_offer?.Cancel();
		_offer = null;
		_offered = null;
		if (interactive is null || current is null || ModeOf(current) != SkipMode.Ask)
		{
			return;
		}

		_offered = current;
		_offer = new CancellationTokenSource();
		_ = OfferAsync(interactive, current, _offer.Token);
	}

	private async Task OfferAsync(IInteractiveMediaPlayer interactive, MediaSegment segment, CancellationToken ct)
	{
		if (await interactive.OfferAsync($"Skip {NameOf(segment)}", ct))
		{
			await player.SeekAsync(segment.End);
		}
	}

	// A recap is skipped like an opening: both come before the episode proper.
	private SkipMode ModeOf(MediaSegment segment) => segment.Type == MediaSegmentType.Ending ? endingMode : openingMode;

	private static string NameOf(MediaSegment segment) => segment.Type switch
	{
		MediaSegmentType.Opening => "opening",
		MediaSegmentType.Ending => "ending",
		_ => "recap",
	};

	private async Task HandleAsync(MediaSegment segment, TimeSpan position)
	{
		var name = NameOf(segment);
		switch (ModeOf(segment))
		{
			case SkipMode.Automatic:
				await player.SeekAsync(segment.End);
				await player.ShowMessageAsync($"Skipped the {name}", TimeSpan.FromSeconds(2));
				break;
			case SkipMode.Ask:
				var left = segment.End - position;
				await player.ShowMessageAsync($"{char.ToUpper(name[0])}{name[1..]}: press Page Up to skip", left < s_hintDuration ? left : s_hintDuration);
				break;
		}
	}

	/// <summary>
	/// The segments as chapters, with "Episode" chapters for what's between them, so each segment ends where the
	/// next chapter starts and the next-chapter key skips exactly to its end.
	/// </summary>
	private static List<MediaChapter> ToChapters(List<MediaSegment> segments, TimeSpan duration)
	{
		List<MediaChapter> chapters = [];
		var cursor = TimeSpan.Zero;
		foreach (var segment in segments.OrderBy(x => x.Start))
		{
			if (segment.Start - cursor > TimeSpan.FromSeconds(1) || chapters.Count == 0 && segment.Start > TimeSpan.Zero)
			{
				chapters.Add(new MediaChapter("Episode", cursor));
			}

			chapters.Add(new MediaChapter(segment.Type.ToString(), segment.Start));
			cursor = segment.End;
		}

		if (duration - cursor > TimeSpan.FromSeconds(1))
		{
			chapters.Add(new MediaChapter("Episode", cursor));
		}

		return chapters;
	}
}
