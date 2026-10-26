namespace Totoro.Core.Modules.Media;

/// <summary>
/// Plays video, e.g. in an external player like mpv. Modules register implementations keyed by their module id.
/// The events may be raised on any thread.
/// </summary>
public interface IMediaPlayer
{
	/// <summary>The module's id.</summary>
	Guid Id { get; }

	string Name { get; }

	event EventHandler<TimeSpan>? DurationChanged;

	event EventHandler<TimeSpan>? PositionChanged;

	/// <summary>Raised when playback ends, e.g. the player window was closed.</summary>
	event EventHandler? PlaybackStopped;

	/// <summary>Whether the player is installed and set up, so <see cref="Play"/> can work.</summary>
	bool IsAvailable();

	/// <summary>Plays <paramref name="media"/> from <paramref name="startPosition"/>, replacing whatever was playing.</summary>
	void Play(MediaItem media, TimeSpan startPosition);

	/// <summary>Jumps to <paramref name="position"/> in what's playing.</summary>
	Task SeekAsync(TimeSpan position);

	/// <summary>Shows <paramref name="text"/> over the video for a while.</summary>
	Task ShowMessageAsync(string text, TimeSpan duration);

	/// <summary>Replaces the chapters of what's playing, e.g. to mark the opening and ending.</summary>
	Task SetChaptersAsync(IReadOnlyList<MediaChapter> chapters);

	/// <summary>The chapters in the video file itself, once it's loaded (its duration is known); none if it has none.</summary>
	Task<IReadOnlyList<MediaChapter>> GetChaptersAsync() => Task.FromResult<IReadOnlyList<MediaChapter>>([]);
}

/// <param name="Title">What the player shows as the title, e.g. "Frieren - Episode 5".</param>
/// <param name="Headers">HTTP headers to send with requests for the video.</param>
/// <param name="IsClip">A short video (e.g. an opening, from the Music tab) rather than an episode: a player with its own controls
/// shows only the simple ones (play, seek, volume, full screen).</param>
public sealed record MediaItem(Uri Uri, string Title, IReadOnlyDictionary<string, string>? Headers = null, bool IsClip = false)
{
	/// <summary>Subtitle files to load beside the video (with <see cref="Headers"/>), the one to show first.</summary>
	public IReadOnlyList<Streaming.SubtitleTrack> Subtitles { get; init; } = [];
}

/// <summary>A chapter starting at <paramref name="Start"/> and lasting until the next one.</summary>
public sealed record MediaChapter(string Title, TimeSpan Start);
