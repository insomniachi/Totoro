namespace Totoro.Core.Modules.Media;

/// <summary>
/// A media player whose controls are the app's own (the built-in player), rather than an external app's: it can offer
/// things over the video itself (skipping the opening, the next episode) instead of in the main window, and its playback
/// can be controlled from the app, e.g. by an agent. The events and methods are used on the UI thread.
/// </summary>
public interface IInteractiveMediaPlayer : IMediaPlayer
{
	/// <summary>Whether its window is open: from <see cref="IMediaPlayer.Play"/> until the user closes it.</summary>
	bool IsOpen { get; }

	bool IsPaused { get; }

	Task SetPausedAsync(bool paused);

	/// <summary>The audio and subtitle tracks of what's playing.</summary>
	IReadOnlyList<MediaTrack> GetTracks();

	/// <summary>Switches to a track from <see cref="GetTracks"/>; a subtitle track's id of -1 turns subtitles off.</summary>
	Task SelectTrackAsync(MediaTrackKind kind, int id);

	/// <summary>
	/// Shows a button over the video (e.g. "Skip opening") for a few seconds of playback, or until <paramref name="ct"/> is
	/// cancelled. True when it's clicked; false when it goes (its time is up, the token, or the window closing).
	/// </summary>
	Task<bool> OfferAsync(string text, CancellationToken ct);

	/// <summary>
	/// Offers the next episode over the video, e.g. "Episode 6", until <paramref name="ct"/> is cancelled. With a
	/// <paramref name="countdown"/>, it's accepted when that runs out. True to play it; false when declined or taken away.
	/// </summary>
	Task<bool> OfferNextAsync(string title, TimeSpan? countdown, CancellationToken ct);

	/// <summary>
	/// Shows what's happening while there's no video to watch, e.g. "Getting episode 6…" or why it can't play; null
	/// clears it. Only while the window is open: it doesn't open it.
	/// </summary>
	void ShowStatus(string? text, bool isError = false);

	/// <summary>
	/// Shows on the seek bar where the episode counts as watched (<paramref name="countsAt"/>, null when it isn't known
	/// yet), and whether it has been marked watched.
	/// </summary>
	void SetWatchedMark(TimeSpan? countsAt, bool isWatched);

	/// <summary>The episodes of what's playing, for the player's episode list and its previous and next buttons.</summary>
	void SetEpisodes(IReadOnlyList<PlayerEpisode> episodes);

	/// <summary>Raised when the user picks an episode (from <see cref="SetEpisodes"/>) to play, by its number.</summary>
	event EventHandler<int>? EpisodeRequested;
}

/// <summary>An episode in the player's episode list.</summary>
/// <param name="Details">e.g. "3 Oct 2025 · 24 min".</param>
/// <param name="Image">Its thumbnail's URL, if there is one.</param>
/// <param name="Progress">How far through it the user stopped, from 0 to 1.</param>
public sealed record PlayerEpisode(
	int Number,
	string Title,
	string Details,
	string Overview,
	string? Image,
	bool IsWatched,
	bool IsCurrent,
	double Progress);

public enum MediaTrackKind
{
	Audio,
	Subtitle,
}

/// <param name="Id">What <see cref="IInteractiveMediaPlayer.SelectTrackAsync"/> takes; -1 is "no subtitles".</param>
/// <param name="Name">What the player calls it, e.g. "English - [eng]".</param>
public sealed record MediaTrack(MediaTrackKind Kind, int Id, string Name, bool IsSelected);
