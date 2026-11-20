using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Services;

namespace Totoro.Agent;

public enum PlaylistControl
{
	Next,
	Previous,
	Pause,
	Resume,
	Stop,
}

/// <summary>Agent tools for the user's playlists of anime songs (see <see cref="SongPlaylistService"/>).</summary>
public sealed class PlaylistTools(SongPlaylistService playlists, ISongPlayer player, AnimeLookup lookup, AnimeSongService songs)
{
	[AgentTool("list_playlists")]
	[Description("The user's playlists of anime songs (openings, endings...), with their songs.")]
	public IReadOnlyList<PlaylistSummary> ListPlaylists() => [.. playlists.Playlists.Select(Summarise)];

	[AgentTool("create_playlist")]
	[Description("Makes a new, empty playlist of anime songs.")]
	public PlaylistSummary CreatePlaylist(string name) => Summarise(Try(() => playlists.Create(name)));

	[AgentTool("add_song_to_playlist")]
	[Description("Adds one of an anime's songs (from get_anime_songs) to the end of a playlist, making the playlist if there's none of that name. Only songs with audio can be added.")]
	public async Task<PlaylistSummary> AddSongToPlaylistAsync(
		string playlist,
		long animeId,
		AnimeSongKind kind,
		[Description("Its number from get_anime_songs; leave it out for the first (or only) one.")] int? number = null)
	{
		var anime = await lookup.GetAsync(animeId);
		var song = AnimeTools.FindSong(anime, await songs.GetSongsAsync(anime), kind, number);
		if (song.Audio is null)
		{
			throw new AgentToolException($"There's no audio of {song.Label}: {song.Title}, so it can't go in a playlist.");
		}

		var list = playlists.Find(playlist) ?? Try(() => playlists.Create(playlist));
		if (!playlists.Add(list, song, anime))
		{
			throw new AgentToolException($"{song.Label}: {song.Title} is in {list.Name} already.");
		}

		return Summarise(list);
	}

	[AgentTool("remove_song_from_playlist")]
	[Description("Takes a song out of a playlist.")]
	public PlaylistSummary RemoveSongFromPlaylist(
		string playlist,
		[Description("Its position in the playlist's songs from list_playlists, from 1.")] int position)
	{
		var list = Get(playlist);
		if (position < 1 || position > list.Songs.Count)
		{
			throw new AgentToolException($"{list.Name} has {list.Songs.Count} songs: give a position from 1 to {list.Songs.Count}.");
		}

		playlists.Remove(list, list.Songs[position - 1]);
		return Summarise(list);
	}

	[AgentTool("delete_playlist")]
	[Description("Deletes a playlist. Ask the user first.")]
	public string DeletePlaylist(string playlist)
	{
		var list = Get(playlist);
		playlists.Delete(list);
		return $"Deleted {list.Name}.";
	}

	[AgentTool("export_playlist")]
	[Description("Saves a playlist to a file (JSON), which import_playlist adds back, e.g. on another PC.")]
	public async Task<string> ExportPlaylistAsync(
		string playlist,
		[Description("The full path of the file to write, e.g. C:\\Users\\me\\Music\\Openings.json. Ask the user where if they didn't say.")] string file)
	{
		var list = Get(playlist);
		try
		{
			await File.WriteAllTextAsync(file, SongPlaylistService.Export(list));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			throw new AgentToolException($"Couldn't save to {file}: {ex.Message}");
		}

		return $"Saved {list.Name} ({list.Songs.Count} songs) to {file}.";
	}

	[AgentTool("import_playlist")]
	[Description("Adds the playlist saved in a file (by export_playlist, or the Playlists page's Export) as a new playlist.")]
	public async Task<PlaylistSummary> ImportPlaylistAsync([Description("The full path of the file.")] string file)
	{
		string text;
		try
		{
			text = await File.ReadAllTextAsync(file);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
		{
			throw new AgentToolException($"Couldn't read {file}: {ex.Message}");
		}

		return Summarise(Try(() => playlists.Import(text)));
	}

	[AgentTool("play_playlist")]
	[Description("Plays a playlist's songs' audio in the app, one after another; it plays on whatever page the user goes to, with controls at the bottom of the window.")]
	public async Task<PlaylistPlaying> PlayPlaylistAsync(
		string playlist,
		[Description("Shuffle it; leave it out to keep the shuffle as it is.")] bool? shuffle = null,
		[Description("Its position from list_playlists to start from, from 1; leave it out for the start (or a random one, shuffled).")] int? from = null)
	{
		var list = Get(playlist);
		await Try(() => playlists.PlayAsync(list, from - 1, shuffle));
		return Playing();
	}

	[AgentTool("control_playlist")]
	[Description("Moves the playlist playing on to the next or previous song, pauses or resumes it, or stops it.")]
	public async Task<PlaylistPlaying> ControlPlaylistAsync(PlaylistControl action)
	{
		if (playlists.Playing is null)
		{
			throw new AgentToolException("No playlist is playing.");
		}

		switch (action)
		{
			case PlaylistControl.Next: await playlists.NextAsync(); break;
			case PlaylistControl.Previous: await playlists.PreviousAsync(); break;
			case PlaylistControl.Pause: player.Pause(); break;
			case PlaylistControl.Resume: player.Resume(); break;
			case PlaylistControl.Stop: playlists.Stop(); break;
		}

		return Playing();
	}

	[AgentTool("set_playlist_mode")]
	[Description("Turns shuffle on or off, or sets how playlists loop. Only the values given change.")]
	public PlaylistPlaying SetPlaylistMode(bool? shuffle = null, PlaylistLoop? loop = null)
	{
		if (shuffle is { } value)
		{
			playlists.Shuffle = value;
		}

		if (loop is { } mode)
		{
			playlists.Loop = mode;
		}

		return Playing();
	}

	private SongPlaylist Get(string name) =>
		playlists.Find(name) ?? throw new AgentToolException(playlists.Playlists.Count == 0
			? "The user has no playlists."
			: $"There's no playlist called {name}. There are: {string.Join(", ", playlists.Playlists.Select(x => x.Name))}.");

	private PlaylistSummary Summarise(SongPlaylist playlist) =>
		new(playlist.Name, [.. playlist.Songs.Select(Describe)], ReferenceEquals(playlists.Playing?.Songs, playlist.Songs));

	private PlaylistPlaying Playing() =>
		new(playlists.Playing?.Name ?? "", playlists.Current is { } song ? Describe(song) : "", playlists.Shuffle, playlists.Loop);

	private static string Describe(PlaylistSong song) => $"{song.Song.Label}: {song.Song.Title} ({song.Anime})";

	private static T Try<T>(Func<T> change)
	{
		try
		{
			return change();
		}
		catch (InvalidOperationException ex)
		{
			throw new AgentToolException(ex.Message);
		}
	}
}
