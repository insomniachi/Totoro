using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Totoro.Core.Services;

/// <summary>
/// The song playing, as the main window's now-playing bar shows it on every page: what it is, how far through, and its
/// controls; next, previous, shuffle and loop while a playlist plays (see <see cref="SongPlaylistService"/>).
/// </summary>
public sealed partial class NowPlaying : ObservableObject, IRecipient<SongPlayerChangedMessage>, IRecipient<SongQueueChangedMessage>
{
	private readonly ISongPlayer _player;
	private readonly SongPlaylistService _playlists;

	public NowPlaying(ISongPlayer player, SongPlaylistService playlists)
	{
		_player = player;
		_playlists = playlists;
		WeakReferenceMessenger.Default.Register<SongPlayerChangedMessage>(this);
		WeakReferenceMessenger.Default.Register<SongQueueChangedMessage>(this);
	}

	/// <summary>Whether a song's playing or paused: the bar shows only then.</summary>
	[ObservableProperty] public partial bool IsOpen { get; private set; }

	[ObservableProperty] public partial string Title { get; private set; } = "";

	/// <summary>e.g. "OP2 · Oshi no Ko · YOASOBI".</summary>
	[ObservableProperty] public partial string Details { get; private set; } = "";

	/// <summary>Its anime's cover, when it's from a playlist; empty otherwise.</summary>
	[ObservableProperty] public partial string Image { get; private set; } = "";

	public bool HasImage => Image.Length > 0;

	public bool HasNoImage => !HasImage;

	/// <summary>The playlist's song playing, whose anime the cover opens; null when it isn't from a playlist.</summary>
	[ObservableProperty] public partial PlaylistSong? Item { get; private set; }

	/// <summary>e.g. "Open Frieren"; the cover's name.</summary>
	public string OpenName => $"Open {Item?.Anime}";

	partial void OnImageChanged(string value)
	{
		OnPropertyChanged(nameof(HasImage));
		OnPropertyChanged(nameof(HasNoImage));
	}

	/// <summary>Whether its anime can be opened: songs added before their anime's ids were kept can't.</summary>
	public bool CanOpen => Item?.Ids is not null;

	partial void OnItemChanged(PlaylistSong? value)
	{
		OnPropertyChanged(nameof(OpenName));
		OnPropertyChanged(nameof(CanOpen));
	}

	[ObservableProperty] public partial bool IsPlaying { get; private set; }

	/// <summary>How far through it, from 0 to 100.</summary>
	[ObservableProperty] public partial double Progress { get; private set; }

	/// <summary><see cref="Progress"/>, for the bar's slider: setting it seeks there.</summary>
	public double SeekProgress
	{
		get => Progress;
		set
		{
			// Ignores the slider echoing back (or snapping) the progress it was just given.
			if (Math.Abs(value - Progress) > 0.5)
			{
				_player.Seek(_player.Duration * (value / 100));
			}
		}
	}

	partial void OnProgressChanged(double value) => OnPropertyChanged(nameof(SeekProgress));

	/// <summary>e.g. "0:42"; empty until its length is known.</summary>
	[ObservableProperty] public partial string PositionText { get; private set; } = "";

	/// <summary>e.g. "1:30"; empty until it's known.</summary>
	[ObservableProperty] public partial string DurationText { get; private set; } = "";

	/// <summary>Whether a playlist's playing: next, previous, shuffle and loop work only then.</summary>
	[ObservableProperty] public partial bool HasPlaylist { get; private set; }

	[ObservableProperty] public partial bool Shuffle { get; private set; }

	public string ShuffleName => Shuffle ? "Shuffle: on" : "Shuffle: off";

	[ObservableProperty] public partial PlaylistLoop Loop { get; private set; }

	public string PlayName => IsPlaying ? "Pause" : "Play";

	public string LoopName => Loop switch
	{
		PlaylistLoop.All => "Loop: the playlist",
		PlaylistLoop.One => "Loop: this song",
		_ => "Loop: off",
	};

	public bool IsLooping => Loop != PlaylistLoop.Off;

	public void Receive(SongPlayerChangedMessage message) => Update();

	public void Receive(SongQueueChangedMessage message) => Update();

	[RelayCommand]
	private void TogglePlay()
	{
		if (_player.IsPlaying)
		{
			_player.Pause();
		}
		else
		{
			_player.Resume();
		}
	}

	[RelayCommand]
	private Task NextAsync() => _playlists.NextAsync();

	[RelayCommand]
	private Task PreviousAsync() => _playlists.PreviousAsync();

	/// <summary>Stops the song, and the playlist if one's playing.</summary>
	[RelayCommand]
	private void Stop() => _playlists.Stop();

	/// <summary>Loop off, then the playlist, then this song, then off again.</summary>
	[RelayCommand]
	private void CycleLoop() => _playlists.Loop = (PlaylistLoop)(((int)_playlists.Loop + 1) % 3);

	[RelayCommand]
	private void ToggleShuffle() => _playlists.Shuffle = !_playlists.Shuffle;

	partial void OnShuffleChanged(bool value) => OnPropertyChanged(nameof(ShuffleName));

	partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlayName));

	partial void OnLoopChanged(PlaylistLoop value)
	{
		OnPropertyChanged(nameof(LoopName));
		OnPropertyChanged(nameof(IsLooping));
	}

	private void Update()
	{
		var song = _player.Current;
		var item = _playlists.Current is { } current && ReferenceEquals(current.Song, song) ? current : null;
		IsOpen = song is not null;
		Title = song?.Title ?? "";
		Details = song is null ? "" : string.Join(" · ", new[] { song.Label, item?.Anime ?? "", song.Artist }.Where(x => x.Length > 0));
		Image = item?.Image ?? "";
		Item = item;
		IsPlaying = _player.IsPlaying;
		Progress = _player.Duration > TimeSpan.Zero ? Math.Clamp(_player.Position / _player.Duration, 0, 1) * 100 : 0;
		var known = _player.Duration > TimeSpan.Zero;
		PositionText = known ? $"{_player.Position:m\\:ss}" : "";
		DurationText = known ? $"{_player.Duration:m\\:ss}" : "";
		HasPlaylist = item is not null;
		Shuffle = _playlists.Shuffle;
		Loop = _playlists.Loop;
	}
}
