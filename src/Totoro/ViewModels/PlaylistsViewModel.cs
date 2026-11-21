using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Services;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>
/// The user's playlists of anime songs (see <see cref="SongPlaylistService"/>): made, renamed and deleted here, their songs
/// found and added, reordered and taken out, and played (songs are added from their cards too, e.g. in the Music tab).
/// </summary>
public sealed partial class PlaylistsViewModel : NavigatableViewModel, IRecipient<SongQueueChangedMessage>
{
	// Typing settles for this long before it's searched for.
	private static readonly TimeSpan s_searchDelay = TimeSpan.FromMilliseconds(300);

	private readonly SongPlaylistService _playlists;
	private readonly AnimeSongService _songs;
	// The search under way, cancelled by the next keystroke.
	private CancellationTokenSource? _search;

	public PlaylistsViewModel(SongPlaylistService playlists, AnimeSongService songs)
	{
		_playlists = playlists;
		_songs = songs;
		Refresh();
		Selected = Playlists.FirstOrDefault();
		WeakReferenceMessenger.Default.Register(this);
	}

	[ObservableProperty] public partial IReadOnlyList<SongPlaylist> Playlists { get; private set; } = [];

	[ObservableProperty] public partial SongPlaylist? Selected { get; set; }

	/// <summary>The selected playlist's songs, which the list reorders by dragging.</summary>
	public ObservableCollection<PlaylistSong> Songs { get; } = [];

	/// <summary>Why the last change couldn't be made; empty, never null, when it could.</summary>
	[ObservableProperty] public partial string Error { get; private set; } = "";

	public bool HasSelected => Selected is not null;

	/// <summary>Songs whose title, singer or anime matches the search, to add: those with audio only, as that's what plays.</summary>
	[ObservableProperty] public partial IReadOnlyList<SongMatch> Found { get; private set; } = [];

	/// <summary>A search is waiting on its results, for the ring in place of the box's search icon.</summary>
	[ObservableProperty] public partial bool IsSearching { get; private set; }

	public void Receive(SongQueueChangedMessage message) => Refresh();

	/// <summary>Finds songs to add, once typing settles; under two letters, none.</summary>
	public async Task SearchAsync(string query)
	{
		_search?.Cancel();
		var cts = _search = new CancellationTokenSource();
		if (query.Trim().Length < 2)
		{
			Found = [];
			IsSearching = false;
			return;
		}

		try
		{
			await Task.Delay(s_searchDelay, cts.Token);
			IsSearching = true;
			var found = await _songs.SearchSongsAsync(query.Trim(), 10, cts.Token);
			if (!cts.IsCancellationRequested)
			{
				Found = [.. found.Where(x => x.Song.Audio is not null)];
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			// The page calls this fire-and-forget: show what went wrong rather than lose it.
			Error = $"Couldn't search for songs: {ex.Message}";
		}
		finally
		{
			// A newer search owns the bar now.
			if (_search == cts)
			{
				IsSearching = false;
			}
		}
	}

	/// <summary>Adds a found song to the end of the selected playlist.</summary>
	public void Add(SongMatch match) => Try(() =>
	{
		if (Selected is not null && !_playlists.Add(Selected, match.Song, match.Anime))
		{
			throw new InvalidOperationException($"{match.Song.Label}: {match.Song.Title} is in {Selected.Name} already.");
		}
	});

	public void Create(string name) => Try(() => Selected = _playlists.Create(name));

	public void Rename(string name) => Try(() => Selected = Selected is null ? null : _playlists.Rename(Selected, name));

	public void Delete() => Try(() =>
	{
		if (Selected is not null)
		{
			_playlists.Delete(Selected);
			Selected = Playlists.FirstOrDefault();
		}
	});

	public void Play(bool shuffle) => Try(() => Selected is null ? Task.CompletedTask : _playlists.PlayAsync(Selected, shuffle: shuffle));

	/// <summary>Plays the selected playlist from <paramref name="song"/>.</summary>
	public void PlayFrom(PlaylistSong song) => Try(() => Selected is null ? Task.CompletedTask : _playlists.PlayAsync(Selected, Selected.Songs.IndexOf(song)));

	public void Remove(PlaylistSong song) => Try(() =>
	{
		if (Selected is not null)
		{
			_playlists.Remove(Selected, song);
		}
	});

	/// <summary>Saves the selected playlist to <paramref name="file"/>, to import elsewhere.</summary>
	public async Task ExportAsync(string file)
	{
		if (Selected is null)
		{
			return;
		}

		try
		{
			await File.WriteAllTextAsync(file, SongPlaylistService.Export(Selected));
			Error = "";
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Error = $"Couldn't save the playlist: {ex.Message}";
		}
	}

	/// <summary>Adds the playlist saved in <paramref name="file"/> (by Export), and selects it.</summary>
	public async Task ImportAsync(string file)
	{
		string text;
		try
		{
			text = await File.ReadAllTextAsync(file);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Error = $"Couldn't read the playlist: {ex.Message}";
			return;
		}

		Try(() => Selected = _playlists.Import(text));
	}

	/// <summary>The list was reordered by dragging <paramref name="song"/> to where it is in <see cref="Songs"/> now.</summary>
	public void Moved(PlaylistSong song)
	{
		if (Selected is not null)
		{
			_playlists.Move(Selected, Selected.Songs.IndexOf(song), Songs.IndexOf(song));
		}
	}

	partial void OnSelectedChanged(SongPlaylist? value)
	{
		OnPropertyChanged(nameof(HasSelected));
		RefreshSongs();
	}

	private void Refresh()
	{
		// Read first: the list clears its selection (and so Selected, bound two-way) when its items are replaced.
		var selected = Selected?.Songs;
		Playlists = [.. _playlists.Playlists];
		// A renamed playlist is a new record with the same songs.
		Selected = Playlists.FirstOrDefault(x => ReferenceEquals(x.Songs, selected));
		RefreshSongs();
	}

	private void RefreshSongs()
	{
		IReadOnlyList<PlaylistSong> songs = Selected?.Songs ?? [];
		if (Songs.SequenceEqual(songs))
		{
			return;
		}

		Songs.Clear();
		foreach (var song in songs)
		{
			Songs.Add(song);
		}
	}

	private void Try(Action change)
	{
		try
		{
			change();
			Error = "";
		}
		catch (InvalidOperationException ex)
		{
			Error = ex.Message;
		}
	}

	private void Try(Func<Task> play) => Try(() => { _ = play(); });
}
