using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Services;

/// <summary>
/// A song as its card shows it (in the Music tab, and in the chat): its audio and its video play in the card. While its audio plays (or is paused), the card shows the spectrum, how far through it is, and the time.
/// </summary>
public sealed partial class SongItem : ObservableObject, IRecipient<SongPlayerChangedMessage>
{
	private readonly AnimeSongService _songs;
	private readonly SongPlaylistService _playlists;
	private readonly AnimeModel? _anime;
	private bool _lyricsAsked;
	private SongLyrics _lyrics = SongLyrics.None;

	/// <param name="anime">The anime it's from, to show it with in a playlist; without it, it can't be added to one.</param>
	public SongItem(AnimeSong song, AnimeSongService songs, ISongPlayer player, SongPlaylistService playlists, AnimeModel? anime)
	{
		_songs = songs;
		_playlists = playlists;
		_anime = anime;
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

	/// <summary>
	/// Its lyrics with their times, once it has played and they're found: the English ones if those are chosen and there
	/// are any. Empty until then, and if there are none.
	/// </summary>
	public IReadOnlyList<LyricLine> Lyrics => Readings == LyricReadings.English && HasEnglish ? _lyrics.English : _lyrics.Lines;

	/// <summary>What its lyrics show.</summary>
	[ObservableProperty] public partial LyricReadings Readings { get; private set; }

	public bool HasLyrics => _lyrics.Lines.Count > 0;

	public bool HasEnglish => _lyrics.English.Count > 0;

	/// <summary>Whether its lyrics are in Japanese script, which readings can be made for; not if they're in romaji already.</summary>
	public bool HasJapanese => _lyrics.Lines.Any(x => x.Text.Any(Services.Lyrics.IsJapanese));

	/// <summary>What its lyrics show, as the button that chooses it says.</summary>
	public string ReadingsName => Readings switch
	{
		LyricReadings.English when HasEnglish => "English",
		_ when !HasJapanese => "Original",
		LyricReadings.Furigana => "Furigana",
		LyricReadings.Romaji => "Romaji",
		LyricReadings.None => "Japanese only",
		_ => "Furigana + romaji",
	};

	/// <summary>Chooses what lyrics show, for every song from now on.</summary>
	public void SetReadings(LyricReadings value) => Readings = _songs.Readings = value;

	partial void OnReadingsChanged(LyricReadings value) => LyricsChanged();

	private void LyricsChanged()
	{
		OnPropertyChanged(nameof(Lyrics));
		OnPropertyChanged(nameof(HasLyrics));
		OnPropertyChanged(nameof(HasEnglish));
		OnPropertyChanged(nameof(ReadingsName));
	}

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

	/// <summary>Whether it can be added to a playlist: its audio is what playlists play.</summary>
	public bool CanAddToPlaylist => HasAudio && _anime is not null;

	/// <summary>The user's playlists, by name, and whether it's in each.</summary>
	public IReadOnlyList<(string Name, bool HasIt)> Playlists => [.. _playlists.Playlists.Select(x => (x.Name, x.Songs.Any(s => s.Song == Song)))];

	/// <summary>Adds it to the playlist of that name, making the playlist if there's none; sets <see cref="Error"/> if it can't.</summary>
	public void AddToPlaylist(string name)
	{
		if (_anime is null)
		{
			return;
		}

		try
		{
			_playlists.Add(_playlists.Find(name) ?? _playlists.Create(name), Song, _anime);
			Error = "";
		}
		catch (InvalidOperationException ex)
		{
			Error = ex.Message;
		}
	}

	/// <summary>Whether its creditless video is open in the card.</summary>
	[ObservableProperty] public partial bool IsVideoOpen { get; set; }

	/// <summary>Opens its creditless video in the card, stopping the audio; closes it if it's open.</summary>
	[RelayCommand]
	private void PlayVideo()
	{
		if (!IsVideoOpen)
		{
			Player.Stop();
			Readings = _songs.Readings;
		}

		IsVideoOpen = !IsVideoOpen;
	}

	/// <summary>Its video has opened, <paramref name="duration"/> long: its lyrics are found by it, as by the audio's.</summary>
	public void VideoOpened(TimeSpan duration)
	{
		if (!_lyricsAsked && duration > TimeSpan.Zero)
		{
			_lyricsAsked = true;
			_ = LoadLyricsAsync(duration);
		}
	}

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
		// Any song's audio starting closes the video, so the two don't play over each other.
		if (Player.IsPlaying)
		{
			IsVideoOpen = false;
		}

		Progress = IsCurrent && Player.Duration > TimeSpan.Zero ? Math.Clamp(Player.Position / Player.Duration, 0, 1) : 0;
		TimeText = IsCurrent && Player.Duration > TimeSpan.Zero ? $"{Player.Position:m\\:ss} / {Player.Duration:m\\:ss}" : "";
		if (IsCurrent)
		{
			Readings = _songs.Readings;
			// Once its length is known: the lyrics that fit it are picked by it.
			if (!_lyricsAsked && Player.Duration > TimeSpan.Zero)
			{
				_lyricsAsked = true;
				_ = LoadLyricsAsync(Player.Duration);
			}
		}
	}

	private async Task LoadLyricsAsync(TimeSpan duration)
	{
		_lyrics = await _songs.GetLyricsAsync(Song, duration);
		LyricsChanged();
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
