using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Services;

/// <summary>
/// A song as its card shows it (in the Music tab, and in the chat): its audio plays in the card, its video in the media
/// player. While its audio plays (or is paused), the card shows the spectrum, how far through it is, and the time.
/// </summary>
public sealed partial class SongItem : ObservableObject, IRecipient<SongPlayerChangedMessage>
{
	private readonly AnimeModel _anime;
	private readonly AnimeSongService _songs;

	public SongItem(AnimeModel anime, AnimeSong song, AnimeSongService songs, ISongPlayer player)
	{
		_anime = anime;
		_songs = songs;
		Song = song;
		Player = player;
		Details = DetailsOf(song);
		Update();
		WeakReferenceMessenger.Default.Register(this);
	}

	public AnimeSong Song { get; }

	/// <summary>The player, for the card's spectrum.</summary>
	public ISongPlayer Player { get; }

	public string Label => Song.Label;

	public string Title => Song.Title;

	/// <summary>Who sings it and the episodes it's in, e.g. "YOASOBI · Episodes 1–16"; empty, never null, if neither is known.</summary>
	public string Details { get; }

	// For x:Bind, which doesn't call a function whose argument is null.
	public bool HasAudio => Song.Audio is not null;

	public bool HasVideo => Song.Video is not null;

	public string VideoName => $"Watch the video of {Label}: {Title}";

	/// <summary>Whether its audio is the one playing or paused.</summary>
	[ObservableProperty] public partial bool IsCurrent { get; private set; }

	[ObservableProperty] public partial bool IsPlaying { get; private set; }

	/// <summary>Whether it's starting: playing, but not heard yet.</summary>
	[ObservableProperty] public partial bool IsLoading { get; private set; }

	/// <summary>Whether it's being heard: the spectrum shows then.</summary>
	[ObservableProperty] public partial bool IsAudible { get; private set; }

	/// <summary>How far through it, from 0 to 1.</summary>
	[ObservableProperty] public partial double Progress { get; private set; }

	/// <summary>e.g. "0:42 / 1:30".</summary>
	[ObservableProperty] public partial string TimeText { get; private set; } = "";

	/// <summary>Why it couldn't be played; empty, never null, when it could.</summary>
	[ObservableProperty] public partial string Error { get; private set; } = "";

	/// <summary>The audio button's glyph, name and tooltip: play, or pause while it plays.</summary>
	public string AudioGlyph => IsPlaying ? "" : "";

	public string AudioName => IsPlaying ? $"Pause {Title}" : $"Play {Label}: {Title}";

	public void Receive(SongPlayerChangedMessage message) => Update();

	/// <summary>Plays its audio; pauses or resumes it if it's the one playing.</summary>
	[RelayCommand]
	private async Task ToggleAudioAsync()
	{
		if (IsCurrent)
		{
			if (IsPlaying)
			{
				Player.Pause();
			}
			else
			{
				Player.Resume();
			}

			return;
		}

		await TryAsync(() => _songs.PlayAudioAsync(Song));
	}

	/// <summary>Plays its creditless video in the media player.</summary>
	[RelayCommand]
	private Task PlayVideoAsync() => TryAsync(() =>
	{
		_songs.PlayVideo(_anime, Song);
		return Task.CompletedTask;
	});

	private async Task TryAsync(Func<Task> play)
	{
		try
		{
			await play();
			Error = "";
		}
		catch (InvalidOperationException ex)
		{
			Error = ex.Message;
		}
	}

	/// <summary>Follows the player: whether it's this song, and how far through it.</summary>
	private void Update()
	{
		IsCurrent = ReferenceEquals(Player.Current, Song);
		IsPlaying = IsCurrent && Player.IsPlaying;
		IsLoading = IsPlaying && Player.IsLoading;
		IsAudible = IsPlaying && !IsLoading;
		Progress = IsCurrent && Player.Duration > TimeSpan.Zero ? Math.Clamp(Player.Position / Player.Duration, 0, 1) : 0;
		TimeText = IsCurrent && Player.Duration > TimeSpan.Zero ? $"{Player.Position:m\\:ss} / {Player.Duration:m\\:ss}" : "";
	}

	partial void OnIsPlayingChanged(bool value)
	{
		OnPropertyChanged(nameof(AudioGlyph));
		OnPropertyChanged(nameof(AudioName));
	}

	private static string DetailsOf(AnimeSong song)
	{
		List<string> details = [];
		if (song.Artist.Length > 0)
		{
			details.Add(song.Artist);
		}

		if (song.Episodes.Length > 0)
		{
			// "Episode 12" or "Episodes 1–16, 18".
			details.Add(song.Episodes.All(char.IsDigit) ? $"Episode {song.Episodes}" : $"Episodes {song.Episodes}");
		}

		return string.Join(" · ", details);
	}
}
