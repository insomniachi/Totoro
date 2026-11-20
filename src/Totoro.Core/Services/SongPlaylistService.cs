using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Helpers;
using Totoro.Core.Modules.Anime;

namespace Totoro.Core.Services;

/// <summary>What a playlist does when it gets to its end, or a song ends.</summary>
public enum PlaylistLoop
{
	[Description("Stop after the last song")] Off,
	[Description("Start over after the last song")] All,
	[Description("Play the same song again and again")] One,
}

/// <summary>A song in a playlist, with its anime's title and cover to show it with, and its ids to open it by.</summary>
/// <param name="Ids">Its anime's ids in each database; null for songs added before they were kept.</param>
public sealed record PlaylistSong(AnimeSong Song, string Anime, string Image, AnimeId? Ids = null)
{
	/// <summary>Its anime, with just its title, cover and ids: to look up in the selected database (it may be another by then).</summary>
	public AnimeModel? ToAnime() => Ids is null ? null : new AnimeModel { Title = Anime, Image = Image, ExternalIds = Ids };
}

/// <summary>A playlist of songs the user picked, in their order.</summary>
public sealed record SongPlaylist(string Name, List<PlaylistSong> Songs);

/// <summary>
/// Sent (with WeakReferenceMessenger.Default, on the UI thread) when the playlists change, or the playlist playing moves
/// on, starts, stops, or its shuffle or loop changes.
/// </summary>
public sealed record SongQueueChangedMessage(SongPlaylistService Queue);

/// <summary>
/// The user's playlists of anime songs (kept in a file), and the one playing: its songs' audio plays one after another in
/// the <see cref="ISongPlayer"/>, in order or shuffled, looping if asked. Playing any other song, or stopping the player,
/// ends the playlist. Used on the UI thread only.
/// </summary>
public sealed class SongPlaylistService : IRecipient<SongPlayerChangedMessage>
{
	// "Previous" this far into a song plays it again from the start, as music players do.
	private static readonly TimeSpan s_restartAfter = TimeSpan.FromSeconds(3);

	private readonly ISongPlayer _player;
	private readonly string _file;
	private readonly List<SongPlaylist> _playlists;
	private readonly Random _random = new();

	// The playing playlist's songs in the order they play (shuffled or not), and where in it the current song is.
	private List<PlaylistSong> _order = [];
	private int _position;
	// Songs that failed in a row: once every song has, it stops rather than going round for ever.
	private int _failures;

	public SongPlaylistService(ISongPlayer player) : this(player, FileHelper.GetPath("playlists.json"))
	{
	}

	internal SongPlaylistService(ISongPlayer player, string file)
	{
		_player = player;
		_file = file;
		try
		{
			_playlists = File.Exists(file) ? JsonSerializer.Deserialize<List<SongPlaylist>>(File.ReadAllText(file)) ?? [] : [];
		}
		catch (JsonException)
		{
			// A damaged file: start over rather than fail.
			_playlists = [];
		}

		player.Ended += (_, _) => OnEnded();
		player.Failed += (_, _) => OnFailed();
		WeakReferenceMessenger.Default.Register(this);
	}

	public IReadOnlyList<SongPlaylist> Playlists => _playlists;

	/// <summary>The playlist playing; null when none is.</summary>
	public SongPlaylist? Playing { get; private set; }

	/// <summary>The playlist's song playing (or paused); null when no playlist is playing.</summary>
	public PlaylistSong? Current { get; private set; }

	/// <summary>Whether the playlists play shuffled; remembered while the app runs.</summary>
	public bool Shuffle
	{
		get;
		set
		{
			if (field == value)
			{
				return;
			}

			field = value;
			if (Playing is not null && Current is not null && Playing.Songs.IndexOf(Current) is var index and >= 0)
			{
				(_order, _position) = PlaylistOrder.Create(Playing.Songs, index, value, _random);
			}

			Changed();
		}
	}

	public PlaylistLoop Loop
	{
		get;
		set
		{
			field = value;
			Changed();
		}
	}

	public SongPlaylist? Find(string name) => _playlists.FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

	/// <summary>A new, empty playlist; throws <see cref="InvalidOperationException"/> if the name's empty or taken.</summary>
	public SongPlaylist Create(string name)
	{
		var playlist = new SongPlaylist(CheckName(name), []);
		_playlists.Add(playlist);
		Save();
		return playlist;
	}

	public SongPlaylist Rename(SongPlaylist playlist, string name)
	{
		if (string.Equals(playlist.Name, name.Trim(), StringComparison.Ordinal))
		{
			return playlist;
		}

		var renamed = playlist with { Name = string.Equals(playlist.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) ? name.Trim() : CheckName(name) };
		_playlists[_playlists.IndexOf(playlist)] = renamed;
		if (ReferenceEquals(Playing, playlist))
		{
			Playing = renamed;
		}

		Save();
		return renamed;
	}

	/// <summary>The playlist as a file's text (JSON), to import elsewhere (another PC, or after a reinstall).</summary>
	public static string Export(SongPlaylist playlist) => JsonSerializer.Serialize(playlist, s_exportOptions);

	/// <summary>
	/// Adds the playlist in a file's text (from <see cref="Export"/>) as a new one, as "Name (2)" if the name's taken.
	/// Songs it can't play (no audio, or not over the web) and repeats are left out. Throws
	/// <see cref="InvalidOperationException"/> if the text isn't a playlist.
	/// </summary>
	public SongPlaylist Import(string text)
	{
		SongPlaylist? read;
		try
		{
			read = JsonSerializer.Deserialize<SongPlaylist>(text);
		}
		catch (JsonException)
		{
			read = null;
		}

		// From a file anyone could have written: every value checked, as JSON's nulls get past non-nullable types.
		if (read?.Songs is not { } songs)
		{
			throw new InvalidOperationException("That file isn't a Totoro playlist.");
		}

		List<PlaylistSong> kept =
		[
			.. songs.Where(x => x is { Anime: not null, Image: not null, Song: { Title: not null, Artist: not null, Episodes: not null, Audio.Scheme: "https" or "http" } })
					.DistinctBy(x => x.Song),
		];
		var name = string.IsNullOrWhiteSpace(read.Name) ? "Imported" : read.Name.Trim();
		var unique = name;
		for (var i = 2; Find(unique) is not null; i++)
		{
			unique = $"{name} ({i})";
		}

		var playlist = new SongPlaylist(unique, kept);
		_playlists.Add(playlist);
		Save();
		return playlist;
	}

	private static readonly JsonSerializerOptions s_exportOptions = new() { WriteIndented = true };

	/// <summary>Deletes the playlist, stopping it if it's playing.</summary>
	public void Delete(SongPlaylist playlist)
	{
		if (ReferenceEquals(Playing, playlist))
		{
			Stop();
		}

		_playlists.Remove(playlist);
		Save();
	}

	/// <summary>Adds the song to the end of the playlist; false if it's there already.</summary>
	public bool Add(SongPlaylist playlist, AnimeSong song, AnimeModel anime)
	{
		if (playlist.Songs.Any(x => x.Song == song))
		{
			return false;
		}

		var item = new PlaylistSong(song, anime.Title, anime.Image, IdsOf(anime));
		playlist.Songs.Add(item);
		if (ReferenceEquals(Playing, playlist))
		{
			_order.Add(item);
		}

		Save();
		return true;
	}

	/// <summary>Removes the song from the playlist; if it's the one playing, it plays on, and then the one after it.</summary>
	public void Remove(SongPlaylist playlist, PlaylistSong song)
	{
		if (!playlist.Songs.Remove(song))
		{
			return;
		}

		if (ReferenceEquals(Playing, playlist) && _order.IndexOf(song) is var index and >= 0)
		{
			_order.RemoveAt(index);
			// The current one's gone: the next plays after it as if it had stayed (from the one now in its place).
			if (index <= _position)
			{
				_position--;
			}
		}

		Save();
	}

	/// <summary>Moves the playlist's song at <paramref name="from"/> to <paramref name="to"/>.</summary>
	public void Move(SongPlaylist playlist, int from, int to)
	{
		if (from == to || from < 0 || to < 0 || from >= playlist.Songs.Count || to >= playlist.Songs.Count)
		{
			return;
		}

		var song = playlist.Songs[from];
		playlist.Songs.RemoveAt(from);
		playlist.Songs.Insert(to, song);
		if (ReferenceEquals(Playing, playlist) && !Shuffle && Current is not null)
		{
			_order = [.. playlist.Songs];
			_position = _order.IndexOf(Current);
		}

		Save();
	}

	/// <summary>
	/// Plays the playlist from its song at <paramref name="start"/> (shuffled from there, if <see cref="Shuffle"/>);
	/// without one, from the first, or a random one if shuffled. <paramref name="shuffle"/> sets it first. Throws
	/// <see cref="InvalidOperationException"/> if it's empty.
	/// </summary>
	public Task PlayAsync(SongPlaylist playlist, int? start = null, bool? shuffle = null)
	{
		if (playlist.Songs.Count == 0)
		{
			throw new InvalidOperationException($"The playlist {playlist.Name} has no songs.");
		}

		if (shuffle is { } value)
		{
			Shuffle = value;
		}

		Playing = playlist;
		_failures = 0;
		var first = start ?? (Shuffle ? _random.Next(playlist.Songs.Count) : 0);
		(_order, _position) = PlaylistOrder.Create(playlist.Songs, Math.Clamp(first, 0, playlist.Songs.Count - 1), Shuffle, _random);
		return PlayAtAsync(_position);
	}

	/// <summary>Plays the next song; after the last, the first if it loops, else it stops.</summary>
	public Task NextAsync()
	{
		_failures = 0;
		return MoveOnAsync();
	}

	/// <summary>Plays the song before; if this one's past its start, plays it again from the start.</summary>
	public Task PreviousAsync()
	{
		if (Current is null)
		{
			return Task.CompletedTask;
		}

		_failures = 0;
		if (_player.Position > s_restartAfter && _order.IndexOf(Current) is var index and >= 0)
		{
			return PlayAtAsync(index);
		}

		return _order.Count == 0 ? Task.CompletedTask : PlayAtAsync(PlaylistOrder.Previous(_position, _order.Count, Loop));
	}

	/// <summary>Stops the playlist and the song playing.</summary>
	public void Stop()
	{
		Clear();
		_player.Stop();
	}

	public void Receive(SongPlayerChangedMessage message)
	{
		// Another song, or none: the playlist's been left (e.g. a song card's play, or stop).
		if (ReferenceEquals(message.Player, _player) && Current is not null && !ReferenceEquals(_player.Current, Current.Song))
		{
			Clear();
		}
	}

	private void OnEnded()
	{
		if (!IsPlayingOurs())
		{
			return;
		}

		_failures = 0;
		// Unless it's been taken out of the playlist meanwhile.
		_ = Loop == PlaylistLoop.One && _order.IndexOf(Current!) is var index and >= 0 ? PlayAtAsync(index) : MoveOnAsync();
	}

	private void OnFailed()
	{
		if (!IsPlayingOurs())
		{
			return;
		}

		if (++_failures >= _order.Count || _order.Count == 0)
		{
			Clear();
			return;
		}

		_ = MoveOnAsync();
	}

	private bool IsPlayingOurs() => Current is not null && ReferenceEquals(_player.Current, Current.Song);

	private Task MoveOnAsync()
	{
		if (Current is null)
		{
			return Task.CompletedTask;
		}

		if (PlaylistOrder.Next(_position, _order.Count, Loop) is { } next)
		{
			return PlayAtAsync(next);
		}

		Stop();
		return Task.CompletedTask;
	}

	private Task PlayAtAsync(int position)
	{
		_position = position;
		// Before playing, so the player's change shows our own song, not another that ends the playlist.
		Current = _order[position];
		Changed();
		return _player.PlayAsync(Current.Song);
	}

	private void Clear()
	{
		if (Playing is null && Current is null)
		{
			return;
		}

		Playing = null;
		Current = null;
		_order = [];
		_position = 0;
		Changed();
	}

	/// <summary>The anime's ids in every database, its own database's among them (its external ids may leave that out).</summary>
	internal static AnimeId IdsOf(AnimeModel anime)
	{
		var known = anime.ExternalIds;
		var ids = new AnimeId
		{
			AniDb = known.AniDb,
			MyAnimeList = known.MyAnimeList,
			Anilist = known.Anilist,
			Kitsu = known.Kitsu,
			Simkl = known.Simkl,
			AnimeNewsNetwork = known.AnimeNewsNetwork,
		};
		if (anime.Id > 0 && typeof(AnimeId).GetProperty(anime.ServiceName ?? "") is { CanWrite: true } own)
		{
			own.SetValue(ids, anime.Id);
		}

		return ids;
	}

	private string CheckName(string name)
	{
		name = name.Trim();
		if (name.Length == 0)
		{
			throw new InvalidOperationException("A playlist needs a name.");
		}

		return Find(name) is not null ? throw new InvalidOperationException($"There's a playlist called {name} already.") : name;
	}

	private void Save()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
		// Written aside, then swapped in: a crash mid-write leaves the old file whole, not a damaged one read as empty.
		var temp = _file + ".tmp";
		File.WriteAllText(temp, JsonSerializer.Serialize(_playlists));
		File.Move(temp, _file, overwrite: true);
		Changed();
	}

	private void Changed() => WeakReferenceMessenger.Default.Send(new SongQueueChangedMessage(this));
}

/// <summary>The order a playlist's songs play in, and where it goes next.</summary>
internal static class PlaylistOrder
{
	/// <summary>
	/// The songs in order, from <paramref name="start"/>; shuffled, <paramref name="start"/>'s song first and the rest in a
	/// random order after it.
	/// </summary>
	public static (List<T> Order, int Position) Create<T>(IReadOnlyList<T> songs, int start, bool shuffle, Random random)
	{
		if (!shuffle)
		{
			return ([.. songs], start);
		}

		var rest = songs.Where((_, i) => i != start).ToArray();
		random.Shuffle(rest);
		return ([songs[start], .. rest], 0);
	}

	/// <summary>The position after <paramref name="position"/>; after the last, the first if it loops, else none.</summary>
	public static int? Next(int position, int count, PlaylistLoop loop) =>
		position + 1 < count ? position + 1 : loop == PlaylistLoop.All && count > 0 ? 0 : null;

	/// <summary>The position before <paramref name="position"/>; before the first, the last if it loops, else the first.</summary>
	public static int Previous(int position, int count, PlaylistLoop loop) =>
		position > 0 ? position - 1 : loop == PlaylistLoop.All ? count - 1 : 0;
}
