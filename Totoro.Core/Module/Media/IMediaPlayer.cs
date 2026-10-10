namespace Totoro.Core.Module.Media;

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
}

/// <param name="Title">What the player shows as the title, e.g. "Frieren - Episode 5".</param>
/// <param name="Headers">HTTP headers to send with requests for the video.</param>
public sealed record MediaItem(Uri Uri, string Title, IReadOnlyDictionary<string, string>? Headers = null);
