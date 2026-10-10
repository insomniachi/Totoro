using Microsoft.Extensions.Logging;
using Totoro.Core.Module.Anime;
using Totoro.Core.Module.Media;
using Totoro.Core.Settings;

namespace Totoro.Playback;

/// <summary>
/// An episode's opening, ending and recap, for one playback: marks them as chapters in the player (so they show on
/// its seek bar, and its next-chapter key skips them), then skips or offers to skip each as playback enters it,
/// following the user's settings. Each segment is handled once, so seeking back into one doesn't skip it again.
/// </summary>
internal sealed class SegmentSkipper(IMediaPlayer player, IPreferences preferences, ILogger logger)
{
	// How long the "press Page Up to skip" hint stays up, at most.
	private static readonly TimeSpan HintDuration = TimeSpan.FromSeconds(8);

	private readonly Lock _lock = new();
	private readonly HashSet<MediaSegment> _handled = [];
	private IReadOnlyList<MediaSegment> _segments = [];

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
		List<MediaSegment> entered;
		lock (_lock)
		{
			// Entered and not nearly over; once each.
			entered = [.. _segments.Where(x => position >= x.Start && position < x.End - TimeSpan.FromSeconds(1) && _handled.Add(x))];
		}

		foreach (var segment in entered)
		{
			_ = HandleAsync(segment, position);
		}
	}

	private async Task HandleAsync(MediaSegment segment, TimeSpan position)
	{
		var name = segment.Type switch
		{
			MediaSegmentType.Opening => "opening",
			MediaSegmentType.Ending => "ending",
			_ => "recap",
		};

		// A recap is skipped like an opening: both come before the episode proper.
		var mode = segment.Type == MediaSegmentType.Ending ? preferences.EndingSkipMode : preferences.OpeningSkipMode;
		switch (mode)
		{
			case SkipMode.Automatic:
				await player.SeekAsync(segment.End);
				await player.ShowMessageAsync($"Skipped the {name}", TimeSpan.FromSeconds(2));
				break;
			case SkipMode.Ask:
				var left = segment.End - position;
				await player.ShowMessageAsync($"{char.ToUpper(name[0])}{name[1..]}: press Page Up to skip", left < HintDuration ? left : HintDuration);
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
