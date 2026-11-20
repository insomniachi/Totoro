using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Xunit;

namespace Totoro.Tests;

public sealed class SongPlaylistTests : IDisposable
{
	private readonly string _file = Path.Combine(Path.GetTempPath(), $"playlists-{Guid.NewGuid():N}.json");
	private readonly FakeSongPlayer _player = new();

	// From AniList, its own id not among its external ids.
	private static readonly AnimeModel s_anime = new()
	{
		Id = 154587,
		ServiceName = nameof(AnimeId.Anilist),
		Title = "Anime",
		Image = "https://example.com/cover.jpg",
		ExternalIds = new AnimeId { MyAnimeList = 52991 },
	};

	public void Dispose() => File.Delete(_file);

	private static AnimeSong Song(int number) =>
		new(AnimeSongKind.Opening, number, $"Song {number}", "Artist", "1–12", null, new Uri($"https://example.com/{number}.ogg"));

	private (SongPlaylistService Service, SongPlaylist Playlist) Make(int songs)
	{
		var service = new SongPlaylistService(_player, _file);
		var playlist = service.Create("Test");
		for (var i = 1; i <= songs; i++)
		{
			service.Add(playlist, Song(i), s_anime);
		}

		return (service, playlist);
	}

	[Fact]
	public void Order_InOrder_StartsAtStart()
	{
		var (order, position) = PlaylistOrder.Create([0, 1, 2, 3], 2, false, new Random(1));
		Assert.Equal([0, 1, 2, 3], order);
		Assert.Equal(2, position);
	}

	[Fact]
	public void Order_Shuffled_StartFirstAndEverySongOnce()
	{
		for (var seed = 0; seed < 20; seed++)
		{
			var (order, position) = PlaylistOrder.Create([0, 1, 2, 3, 4, 5], 3, true, new Random(seed));
			Assert.Equal(0, position);
			Assert.Equal(3, order[0]);
			Assert.Equal([0, 1, 2, 3, 4, 5], order.Order());
		}
	}

	[Theory]
	[InlineData(0, 3, PlaylistLoop.Off, 1)]
	[InlineData(2, 3, PlaylistLoop.Off, null)]
	[InlineData(2, 3, PlaylistLoop.All, 0)]
	[InlineData(2, 3, PlaylistLoop.One, null)]
	public void Order_Next(int position, int count, PlaylistLoop loop, int? expected) =>
		Assert.Equal(expected, PlaylistOrder.Next(position, count, loop));

	[Theory]
	[InlineData(1, 3, PlaylistLoop.Off, 0)]
	[InlineData(0, 3, PlaylistLoop.Off, 0)]
	[InlineData(0, 3, PlaylistLoop.All, 2)]
	public void Order_Previous(int position, int count, PlaylistLoop loop, int expected) =>
		Assert.Equal(expected, PlaylistOrder.Previous(position, count, loop));

	[Fact]
	public async Task Ended_PlaysNext_ThenStopsAfterLast()
	{
		var (service, playlist) = Make(2);
		await service.PlayAsync(playlist);
		Assert.Equal(1, _player.Current?.Number);

		_player.End();
		Assert.Equal(2, _player.Current?.Number);
		Assert.Same(playlist, service.Playing);

		_player.End();
		Assert.Null(_player.Current);
		Assert.Null(service.Playing);
	}

	[Fact]
	public async Task Ended_LoopAll_StartsOver_LoopOne_Repeats()
	{
		var (service, playlist) = Make(2);
		service.Loop = PlaylistLoop.All;
		await service.PlayAsync(playlist, 1);
		_player.End();
		Assert.Equal(1, _player.Current?.Number);

		service.Loop = PlaylistLoop.One;
		_player.End();
		Assert.Equal(1, _player.Current?.Number);
		Assert.Equal(3, _player.Plays);
		await service.NextAsync();
		Assert.Equal(2, _player.Current?.Number);
	}

	[Fact]
	public async Task UserStop_OrAnotherSong_EndsPlaylist()
	{
		var (service, playlist) = Make(2);
		await service.PlayAsync(playlist);
		_player.Stop();
		Assert.Null(service.Playing);

		await service.PlayAsync(playlist);
		await _player.PlayAsync(Song(9));
		Assert.Null(service.Playing);
		_player.End();
		Assert.Null(_player.Current);
	}

	[Fact]
	public async Task Failed_SkipsSong_AndStopsWhenAllFail()
	{
		var (service, playlist) = Make(3);
		service.Loop = PlaylistLoop.All;
		await service.PlayAsync(playlist);
		_player.Fail();
		Assert.Equal(2, _player.Current?.Number);
		_player.Fail();
		_player.Fail();
		Assert.Null(_player.Current);
		Assert.Null(service.Playing);
	}

	[Fact]
	public async Task Shuffle_Off_ContinuesInOrderFromCurrent()
	{
		var (service, playlist) = Make(4);
		await service.PlayAsync(playlist, 2, shuffle: true);
		Assert.Equal(3, _player.Current?.Number);
		service.Shuffle = false;
		await service.NextAsync();
		Assert.Equal(4, _player.Current?.Number);
	}

	[Fact]
	public async Task Remove_Current_NextPlaysTheOneAfterIt()
	{
		var (service, playlist) = Make(3);
		await service.PlayAsync(playlist);
		service.Remove(playlist, playlist.Songs[0]);
		_player.End();
		Assert.Equal(2, _player.Current?.Number);
	}

	[Fact]
	public void Playlists_AreSavedAndLoaded()
	{
		var (service, playlist) = Make(2);
		Assert.False(service.Add(playlist, Song(1), s_anime));
		service.Rename(playlist, "Renamed");

		var loaded = new SongPlaylistService(new FakeSongPlayer(), _file).Playlists.Single();
		Assert.Equal("Renamed", loaded.Name);
		Assert.Equal([Song(1), Song(2)], loaded.Songs.Select(x => x.Song));
		Assert.Equal("OP1", loaded.Songs[0].Song.Label);
		// Its anime, by its ids in both databases, to open in whichever is selected.
		var anime = loaded.Songs[0].ToAnime();
		Assert.NotNull(anime);
		Assert.Equal("Anime", anime.Title);
		Assert.Equal(154587, anime.ExternalIds.Anilist);
		Assert.Equal(52991, anime.ExternalIds.MyAnimeList);
	}

	[Fact]
	public void DamagedFile_StartsEmpty()
	{
		File.WriteAllText(_file, "{ not json");
		Assert.Empty(new SongPlaylistService(_player, _file).Playlists);
	}

	[Fact]
	public void Create_TakenOrEmptyName_Throws()
	{
		var (service, _) = Make(0);
		Assert.Throws<InvalidOperationException>(() => service.Create(" test "));
		Assert.Throws<InvalidOperationException>(() => service.Create(" "));
	}

	/// <summary>Plays nothing: follows <see cref="ISongPlayer"/>'s contract as the real one does, ending a song when told.</summary>
	private sealed class FakeSongPlayer : ISongPlayer
	{
		public AnimeSong? Current { get; private set; }

		public bool IsPlaying => Current is not null;

		public bool IsLoading => false;

		public TimeSpan Position => TimeSpan.Zero;

		public TimeSpan Duration => TimeSpan.Zero;

		public int Plays { get; private set; }

		public event EventHandler? Ended;

		public event EventHandler? Failed;

		public Task PlayAsync(AnimeSong song)
		{
			Current = song;
			Plays++;
			Changed();
			return Task.CompletedTask;
		}

		public void Pause()
		{
		}

		public void Resume()
		{
		}

		public void Stop()
		{
			Current = null;
			Changed();
		}

		public void GetLevels(Span<float> bands)
		{
		}

		public void End() => Finish(Ended);

		public void Fail() => Finish(Failed);

		// As SongAudioPlayer: the event first, then it stops unless a handler played another song.
		private void Finish(EventHandler? finished)
		{
			var plays = Plays;
			finished?.Invoke(this, EventArgs.Empty);
			if (Plays == plays)
			{
				Stop();
			}
		}

		private void Changed() => WeakReferenceMessenger.Default.Send(new SongPlayerChangedMessage(this));
	}
}
