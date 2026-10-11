using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Services;

namespace Totoro.ViewModels.AnimeDetails;

/// <summary>
/// A song as the Music tab shows it: while its audio plays (or is paused), its card shows the spectrum, how far through
/// it is, and the time.
/// </summary>
/// <param name="Details">Who sings it and the episodes it's in, e.g. "YOASOBI · Episodes 1–16".</param>
public sealed partial class SongItem(AnimeSong song, string details, ISongPlayer player) : ObservableObject
{
	public AnimeSong Song { get; } = song;

	/// <summary>The player, for the card's spectrum.</summary>
	public ISongPlayer Player { get; } = player;

	public string Label => Song.Label;

	public string Title => Song.Title;

	public string Details { get; } = details;

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

	/// <summary>The audio button's glyph, name and tooltip: play, or pause while it plays.</summary>
	public string AudioGlyph => IsPlaying ? "" : "";

	public string AudioName => IsPlaying ? $"Pause {Title}" : $"Play {Label}: {Title}";

	/// <summary>Follows the player: whether it's this song, and how far through it.</summary>
	public void Update()
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
}

/// <param name="Header">e.g. "Openings".</param>
public sealed record SongGroup(string Header, IReadOnlyList<SongItem> Songs);

/// <summary>
/// The anime's songs (see <see cref="AnimeSongService"/>): its openings, endings and insert songs, each group under its
/// heading. A song's audio plays in its card; its video in the media player.
/// </summary>
public sealed partial class MusicTab : LazyDetailsTab, IRecipient<SongPlayerChangedMessage>
{
	private readonly Func<Task<IReadOnlyList<AnimeSong>>> _fetch;
	private readonly Func<AnimeSong, Task> _playAudio;
	private readonly Action<AnimeSong> _playVideo;
	private readonly ISongPlayer _player;

	public MusicTab(Func<Task<IReadOnlyList<AnimeSong>>> fetch, Func<AnimeSong, Task> playAudio, Action<AnimeSong> playVideo, ISongPlayer player,
					ILogger logger)
		: base("No songs listed.", logger)
	{
		_fetch = fetch;
		_playAudio = playAudio;
		_playVideo = playVideo;
		_player = player;
		WeakReferenceMessenger.Default.Register(this);
	}

	[ObservableProperty]
	public partial IReadOnlyList<SongGroup> Groups { get; private set; } = [];

	/// <summary>Why the last song couldn't be played; empty, never null, when it could.</summary>
	[ObservableProperty]
	public partial string PlayError { get; private set; } = "";

	public void Receive(SongPlayerChangedMessage message)
	{
		foreach (var song in Groups.SelectMany(x => x.Songs))
		{
			song.Update();
		}
	}

	/// <summary>Stops the song playing, e.g. when the tab is left.</summary>
	public void StopAudio()
	{
		if (Groups.SelectMany(x => x.Songs).Any(x => x.IsCurrent))
		{
			_player.Stop();
		}
	}

	protected override async Task<int> LoadAsync()
	{
		var songs = await _fetch();
		Groups =
		[
			.. songs.GroupBy(x => x.Kind)
					.Select(x => new SongGroup(HeaderOf(x.Key), [.. x.Select(ToItem)]))
		];
		Receive(new SongPlayerChangedMessage(_player));
		return songs.Count;
	}

	/// <summary>Plays the song's audio in its card; pauses or resumes it if it's the one playing.</summary>
	[RelayCommand]
	private async Task ToggleAudioAsync(SongItem item)
	{
		if (item.IsCurrent)
		{
			if (item.IsPlaying)
			{
				_player.Pause();
			}
			else
			{
				_player.Resume();
			}

			return;
		}

		await TryAsync(() => _playAudio(item.Song));
	}

	[RelayCommand]
	private Task PlayVideoAsync(SongItem item) => TryAsync(() =>
	{
		_playVideo(item.Song);
		return Task.CompletedTask;
	});

	private async Task TryAsync(Func<Task> play)
	{
		try
		{
			await play();
			PlayError = "";
		}
		catch (InvalidOperationException ex)
		{
			PlayError = ex.Message;
		}
	}

	private static string HeaderOf(AnimeSongKind kind) => kind switch
	{
		AnimeSongKind.Opening => "Openings",
		AnimeSongKind.Ending => "Endings",
		_ => "Insert songs",
	};

	private SongItem ToItem(AnimeSong song)
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

		return new SongItem(song, string.Join(" · ", details), _player);
	}
}
