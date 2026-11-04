using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Settings;

namespace Totoro.Playback;

/// <summary>
/// Watching together over Syncplay (syncplay.pl): joins a room on a Syncplay server and keeps the episode playing in step
/// with everyone in it, pausing, resuming and seeking when anyone does, in Totoro or in Syncplay's own app. Its protocol is
/// JSON messages, a line each, over TCP: the server sends where the room is about once a second, and each client answers
/// with where it is, or says so straight away when its user pauses or seeks.
/// </summary>
public sealed partial class SyncplayClient : ObservableObject
{
	// The protocol version the server checks, and the Syncplay release it's from.
	private const string ProtocolVersion = "1.2.255";
	private const string ReleaseVersion = "1.7.4";

	// While paused (or told someone seeked), this far from where it should be is a seek.
	private static readonly TimeSpan s_seekThreshold = TimeSpan.FromSeconds(1.5);

	// While playing, this far behind or ahead of the room it seeks to where the room is (Syncplay's own rewind threshold),
	// and a jump this far is the user seeking.
	private static readonly TimeSpan s_desyncThreshold = TimeSpan.FromSeconds(4);

	// After following the room (or an episode starting), the player's given this long to get there before changes count as the user's.
	private const double SettleSeconds = 1.5;

	private static readonly TimeSpan s_messageDuration = TimeSpan.FromSeconds(3);

	private readonly WatchService _watch;
	private readonly IPreferences _preferences;
	private readonly ILogger<SyncplayClient> _logger;
	private readonly IFactory<IAnimeMetadataProvider, Guid> _databases;
	private readonly DispatcherQueueTimer _timer;

	// After an episode starts here, others' for a while aren't followed: two started at once would otherwise swap back and forth.
	private const double FollowEpisodesAfterSeconds = 5;
	private double _followEpisodesAfter;
	private readonly SemaphoreSlim _writeLock = new(1, 1);
	private TcpClient? _tcp;
	private CancellationTokenSource? _cts;
	private string _name = "";
	private string _room = "";

	// The others in the room, with the episode each is playing ("" for none).
	private readonly Dictionary<string, string> _people = [];

	// The episodes the others are playing in Totoro (what playing it here takes), and in what order they started them.
	private readonly Dictionary<string, (JsonObject Episode, string File, int Order)> _episodes = [];
	private int _episodeOrder;

	private IMediaPlayer? _player;
	private string? _file;
	private long _positionTicks;
	private (string Name, int Seconds)? _fileSent;

	// An episode just started that nobody else was on: once it settles, the room goes to where it is. Decided as it starts,
	// since others following it start it too (and would otherwise make it follow the room, paused where it was).
	private bool _announce;

	// The player as of the last check, to tell the user's own pauses and seeks from playing on.
	private TimeSpan _lastPosition;
	private bool _lastPaused;
	private double _lastAt;
	private double _settleUntil;

	// Changes sent and not yet acknowledged by the server (the room's state is ignored meanwhile), and the server's to acknowledge.
	private int _clientIgnoring;
	private int _serverIgnoring;
	private double _rtt;

	// The room as the server last said it is, to say back while nothing's playing here (see SendStateAsync).
	private (double Position, bool Paused, double At)? _roomState;

	public SyncplayClient(WatchService watch, IPreferences preferences, IFactory<IAnimeMetadataProvider, Guid> databases, ILogger<SyncplayClient> logger)
	{
		_watch = watch;
		_preferences = preferences;
		_databases = databases;
		_logger = logger;
		// Made on the UI thread, where WatchService changes; the connection's reads carry on there too.
		_timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
		_timer.Interval = TimeSpan.FromMilliseconds(250);
		_timer.Tick += (_, _) => Tick();
	}

	/// <summary>Whether it's in a room (or joining one): turning it on joins <see cref="Room"/>, off leaves it.</summary>
	[ObservableProperty]
	public partial bool IsOn { get; set; }

	/// <summary>What's happening, e.g. "Watching with Alice, Bob in anime-night", or why it stopped.</summary>
	[ObservableProperty]
	public partial string Status { get; private set; } = "";

	/// <summary>The room it joins: everyone in it plays in step. Changing it applies when it next joins.</summary>
	public string Room
	{
		get => _preferences.SyncplayRoom;
		set => Save(Room, value, x => _preferences.SyncplayRoom = x);
	}

	/// <summary>The name the others in the room see.</summary>
	public string Name
	{
		get => _preferences.SyncplayName;
		set => Save(Name, value, x => _preferences.SyncplayName = x);
	}

	/// <summary>The Syncplay server, as host:port.</summary>
	public string Server
	{
		get => _preferences.SyncplayServer;
		set => Save(Server, value, x => _preferences.SyncplayServer = x);
	}

	private void Save(string old, string value, Action<string> save, [CallerMemberName] string property = "")
	{
		if (old != value)
		{
			save(value);
			OnPropertyChanged(property);
		}
	}

	/// <summary>The others in the room.</summary>
	public IReadOnlyCollection<string> People => _people.Keys;

	// Set while JoinAsync waits: done once in the room, or it couldn't get in.
	private TaskCompletionSource? _joining;

	/// <summary>(Re)joins <see cref="Room"/>, once it's in or couldn't get in; says how it went, as <see cref="Status"/>.</summary>
	public async Task<string> JoinAsync()
	{
		IsOn = false;
		var joining = _joining = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		IsOn = true;
		try
		{
			await joining.Task.WaitAsync(TimeSpan.FromSeconds(15));
		}
		catch (TimeoutException)
		{
			// Still joining: the status says so.
		}

		return Status;
	}

	partial void OnIsOnChanged(bool value)
	{
		if (value)
		{
			_ = ConnectAsync();
		}
		else
		{
			Disconnect("");
		}
	}

	private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

	// Zero until the player says where it is.
	private TimeSpan Position => new(Math.Max(0, Interlocked.Read(ref _positionTicks)));

	private async Task ConnectAsync()
	{
		_name = Name.Trim();
		_room = Room.Trim();
		if (!TryParseServer(Server, out var host, out var port) || _name.Length == 0 || _room.Length == 0)
		{
			Disconnect("Fill in the server (host:port), the room and your name first.");
			return;
		}

		var cts = _cts = new CancellationTokenSource();
		Status = $"Joining {_room}…";
		try
		{
			var tcp = _tcp = new TcpClient();
			await tcp.ConnectAsync(host, port, cts.Token);
			await SendAsync(new JsonObject
			{
				["Hello"] = new JsonObject
				{
					["username"] = _name,
					["room"] = new JsonObject { ["name"] = _room },
					["version"] = ProtocolVersion,
					["realversion"] = ReleaseVersion,
					["features"] = new JsonObject { ["sharedPlaylists"] = false, ["chat"] = true, ["readiness"] = true, ["managedRooms"] = false },
				},
			});

			using var reader = new StreamReader(tcp.GetStream(), Encoding.UTF8);
			while (await reader.ReadLineAsync(cts.Token) is { } line)
			{
				Handle(JsonNode.Parse(line));
			}

			if (!cts.IsCancellationRequested)
			{
				Disconnect("The server closed the connection.");
			}
		}
		catch (Exception) when (cts.IsCancellationRequested)
		{
			// Left the room.
		}
		catch (Exception ex)
		{
			// shortcut: doesn't reconnect by itself; turning it back on does.
			_logger.LogWarning(ex, "Lost the Syncplay server");
			Disconnect($"Lost {host}: {ex.Message}");
		}
	}

	private void Disconnect(string status)
	{
		_cts?.Cancel();
		_cts = null;
		_tcp?.Dispose();
		_tcp = null;
		_timer.Stop();
		_people.Clear();
		_episodes.Clear();
		if (_player is not null)
		{
			_player.PositionChanged -= OnPosition;
			(_player as IInteractiveMediaPlayer)?.SetRoomPeople(null);
		}

		(_player, _file, _fileSent, _roomState) = (null, null, null, null);
		(_clientIgnoring, _serverIgnoring) = (0, 0);
		// Comes back here with no status when it was on: the one given is said after.
		IsOn = false;
		Status = status;
		UpdateCatchUp();
		_joining?.TrySetResult();
	}

	private void Handle(JsonNode? message)
	{
		if (message?["Hello"] is JsonObject hello)
		{
			// The server changes the name if someone in the room has it already.
			_name = (string?)hello["username"] ?? _name;
			_timer.Start();
			_ = SendAsync(new JsonObject { ["List"] = null });
			ShowPeople();
		}
		else if (message?["Set"]?["user"] is JsonObject users)
		{
			foreach (var (name, user) in users)
			{
				OnPerson(name, user);
			}
		}
		else if (message?["List"]?[_room] is JsonObject list)
		{
			_people.Clear();
			_episodes.Clear();
			foreach (var (name, user) in list.Where(x => x.Key != _name))
			{
				_people[name] = (string?)user?["file"]?["name"] ?? "";
				RememberEpisode(name, user?["file"]);
			}

			ShowPeople();
			// In, knowing who else is.
			_joining?.TrySetResult();

			// (Asked for once, on joining.) Someone's already watching an episode in Totoro: it starts here too, and
			// catches up with them, unless something's playing here.
			if (!_watch.IsActive)
			{
				_ = CatchUpAsync();
			}
		}
		else if (message?["Chat"] is JsonObject chat && (string?)chat["username"] is { } from && from != _name && (string?)chat["message"] is { } said)
		{
			Notice(from, said.StartsWith(CouldNotPlay, StringComparison.Ordinal) ? said : $"says “{said}”");
		}
		else if (message?["Error"] is JsonObject error)
		{
			Disconnect((string?)error["message"] ?? "The server turned it down.");
		}
		else if (message?["State"] is JsonObject state)
		{
			OnState(state);
		}
	}

	private void OnPerson(string name, JsonNode? person)
	{
		if (name == _name)
		{
			return;
		}

		if ((string?)person?["room"]?["name"] is { } room && room != _room)
		{
			// Moved to another room (or is in one): not watching with us.
			_episodes.Remove(name);
			if (_people.Remove(name))
			{
				ShowPeople();
			}

			return;
		}

		if (person?["event"] is JsonObject happened)
		{
			if (happened.ContainsKey("left"))
			{
				_people.Remove(name);
				_episodes.Remove(name);
				Notice(name, "left the room");
			}
			else if (happened.ContainsKey("joined"))
			{
				_people[name] = "";
				Notice(name, "joined the room");
			}

			ShowPeople();
		}

		if ((string?)person?["file"]?["name"] is { } file && _people.GetValueOrDefault(name) != file)
		{
			_people[name] = file;
			RememberEpisode(name, person?["file"]);
			ShowPeople();
			if (file.Length == 0)
			{
				// Closed their player, still in the room (see Tick).
				Notice(name, "stopped watching");
				return;
			}

			Say($"{name} is playing {file}");
			if (person?["file"]?["totoro"] is JsonObject episode)
			{
				_ = PlayAsync(name, (string?)episode["service"], (long?)episode["id"] ?? 0, (int?)episode["episode"] ?? 0);
			}
		}
	}

	/// <summary>Keeps (or forgets) the episode someone's playing in Totoro, from the file they said they're playing.</summary>
	private void RememberEpisode(string name, JsonNode? file)
	{
		if (file?["totoro"] is JsonObject episode && (string?)file["name"] is { Length: > 0 } title)
		{
			_episodes[name] = ((JsonObject)episode.DeepClone(), title, ++_episodeOrder);
		}
		else
		{
			_episodes.Remove(name);
		}
	}

	/// <summary>The episode the last of the others to start one is playing, if any of them is playing one in Totoro.</summary>
	private (string By, JsonObject Episode, string File)? Latest =>
		_episodes.Count == 0 ? null : _episodes.MaxBy(x => x.Value.Order) is var (by, (episode, file, _)) ? (by, episode, file) : null;

	/// <summary>
	/// What <see cref="CatchUpAsync"/> would play, e.g. "alice is watching Frieren · Episode 5"; empty when there's nothing
	/// to catch up with (nobody else is watching anything in Totoro) or something's playing here.
	/// </summary>
	[ObservableProperty]
	public partial string CatchUpText { get; private set; } = "";

	private void UpdateCatchUp() => CatchUpText = IsOn && !_watch.IsActive && Latest is { } latest ? $"{latest.By} is watching {latest.File}" : "";

	/// <summary>Plays the episode the last of the others to start one is watching, from where they are (see <see cref="CatchUpText"/>).</summary>
	public Task CatchUpAsync()
	{
		return Latest is ({ } by, { } episode, _)
			? PlayAsync(by, (string?)episode["service"], (long?)episode["id"] ?? 0, (int?)episode["episode"] ?? 0, asked: true)
			: Task.CompletedTask;
	}

	/// <summary>Plays the episode someone in the room started (by its id in their anime database), unless it's playing already.</summary>
	/// <param name="asked">Whether the user asked for it, rather than following someone (which waits after an episode starts here).</param>
	private async Task PlayAsync(string by, string? service, long id, int episode, bool asked = false)
	{
		if (service is null || id <= 0 || episode <= 0 || !asked && Now < _followEpisodesAfter ||
			_watch.IsActive && _watch.Anime is { } playing && playing.ServiceName == service && playing.Id == id && _watch.CurrentEpisode == episode)
		{
			return;
		}

		try
		{
			if (_databases.CreateDefault() is not { } database)
			{
				return;
			}

			// Their database may not be this one: ani.zip maps the ids.
			var ownId = database.Name == service ? id : await new AnimeModel { Id = id, ServiceName = service }.FindIdAsync(database.Name);
			if (ownId is not > 0)
			{
				Status = $"Couldn't find what {by} is playing on {database.Name}.";
				_ = TellAsync($"{CouldNotPlay}episode {episode}: it isn't on {database.Name}");
				return;
			}

			var anime = await database.GetAnimeAsync(ownId.Value);
			await _watch.WatchAsync(anime, episode);
			// It's left in the status bar when it couldn't play (no provider, not on it...): the others are told why.
			if (_watch.Kind == PlaybackStatusKind.Error && ReferenceEquals(_watch.Anime, anime) && _watch.CurrentEpisode == episode)
			{
				_ = TellAsync($"{CouldNotPlay}{_watch.Title}: {FirstSentence(_watch.Message)}");
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Couldn't play what {Person} started", by);
			Status = $"Couldn't play what {by} started: {ex.Message}";
			_ = TellAsync($"{CouldNotPlay}episode {episode}: {ex.Message}");
		}
	}

	// What a chat message starts with when Totoro couldn't play an episode someone started; shown as it is, after the name.
	private const string CouldNotPlay = "couldn't play ";

	/// <summary>Says something to the room, in Syncplay's chat (which Syncplay's own app shows too).</summary>
	private Task TellAsync(string text) => SendAsync(new JsonObject { ["Chat"] = FitChat(text) });

	// Syncplay's server cuts chat messages at this many characters.
	private const int MaxChatLength = 150;

	/// <summary>Up to the end of the first sentence, e.g. of a status's reason (the rest is how to fix it, for the user here).</summary>
	internal static string FirstSentence(string text) => text.IndexOf(". ", StringComparison.Ordinal) is > 0 and var end ? text[..(end + 1)] : text;

	/// <summary>Short enough for Syncplay's chat, ending in "…" where it's cut.</summary>
	internal static string FitChat(string text) => text.Length <= MaxChatLength ? text : text[..(MaxChatLength - 1)].TrimEnd() + "…";

	private void OnState(JsonObject state)
	{
		if (state["ignoringOnTheFly"] is JsonObject ignoring)
		{
			if (ignoring["server"] is { } server)
			{
				_serverIgnoring = (int)server;
				_clientIgnoring = 0;
			}
			else if (ignoring["client"] is { } client && (int)client == _clientIgnoring)
			{
				_clientIgnoring = 0;
			}
		}

		var latency = (double?)state["ping"]?["latencyCalculation"];
		if ((double?)state["ping"]?["clientLatencyCalculation"] is double sentAt && sentAt > 0)
		{
			_rtt = Now - sentAt;
		}

		// Not while a change of ours is on its way, nor while the player's still getting where it was sent, nor when this
		// episode's just started and nobody else was on it (the room follows it instead). Nor the room as we set it: the
		// player may not say it's paused yet, and following it would hold off the user's next change.
		if (state["playstate"] is JsonObject roomPlay)
		{
			_roomState = (Math.Max(0, (double?)roomPlay["position"] ?? 0), (bool?)roomPlay["paused"] ?? true, Now);
		}

		if (state["playstate"] is JsonObject play && (string?)play["setBy"] is var by && by != _name &&
			_clientIgnoring == 0 && _player is { } player && Now >= _settleUntil && !_announce)
		{
			var paused = (bool?)play["paused"] ?? true;
			var doSeek = (bool?)play["doSeek"] ?? false;
			// shortcut: takes half the round trip as how old the message is, where Syncplay averages; fine on a steady connection.
			var room = TimeSpan.FromSeconds(Math.Max(0, ((double?)play["position"] ?? 0) + (paused ? 0 : _rtt / 2)));
			var (pause, seekTo) = Follow(Position, player.IsPaused, room, paused, doSeek);
			if (pause is { } p)
			{
				_ = player.SetPausedAsync(p);
			}

			if (seekTo is { } to)
			{
				_ = player.SeekAsync(to);
			}

			if (pause is not null || seekTo is not null)
			{
				_settleUntil = Now + SettleSeconds;
				_announce = false;
				if (by is { Length: > 0 })
				{
					Say(pause is { } nowPaused ? $"{by} {(nowPaused ? "paused" : "resumed")}" : doSeek ? $"{by} jumped to {Clock(room)}" : $"Caught up with {by}");
				}
			}
		}

		_ = SendStateAsync(latency, change: false);
	}

	/// <summary>Follows the player playing (a new episode starts over), and says what the user did in it.</summary>
	private void Tick()
	{
		UpdateCatchUp();
		var player = _watch.PlayingPlayer;
		var file = player is null ? null : _watch.Title;
		if (!ReferenceEquals(player, _player) || file != _file)
		{
			if (_player is not null)
			{
				_player.PositionChanged -= OnPosition;
				if (player is null)
				{
					// Closed (or finished) while in the room: no longer watching, which the others see.
					_fileSent = null;
					_ = SendAsync(new JsonObject { ["Set"] = new JsonObject { ["file"] = new JsonObject { ["name"] = "", ["duration"] = 0, ["size"] = 0 } } });
				}
			}

			(_player, _file) = (player, file);
			if (player is not null)
			{
				Interlocked.Exchange(ref _positionTicks, -1);
				player.PositionChanged += OnPosition;
				_announce = !OthersOnFile;
				_followEpisodesAfter = Now + FollowEpisodesAfterSeconds;
			}

			ShowPeople();
		}

		if (player is null)
		{
			return;
		}

		// Until it says where it is, the player's still opening the episode (and may start playing over a pause): what it
		// does then isn't the user's doing, nor for a while after.
		if (Interlocked.Read(ref _positionTicks) < 0)
		{
			_settleUntil = Now + SettleSeconds;
		}

		var (now, position, paused) = (Now, Position, player.IsPaused);
		var duration = (int)_watch.PlaybackDuration.TotalSeconds;
		if (_fileSent != (file!, duration))
		{
			_fileSent = (file!, duration);
			var sent = new JsonObject { ["name"] = file, ["duration"] = duration, ["size"] = 0 };
			if (_watch.Anime is { ServiceName: { } service } anime)
			{
				// Which episode, for the others in Totoro to play it too (the server passes it on as it is; Syncplay's app ignores it).
				sent["totoro"] = new JsonObject { ["service"] = service, ["id"] = anime.Id, ["episode"] = _watch.CurrentEpisode };
			}

			_ = SendAsync(new JsonObject { ["Set"] = new JsonObject { ["file"] = sent } });
			_ = SendAsync(new JsonObject { ["Set"] = new JsonObject { ["ready"] = new JsonObject { ["isReady"] = true, ["manuallyInitiated"] = false } } });
		}

		if (now >= _settleUntil)
		{
			var seeked = IsSeek(_lastPosition, _lastPaused, TimeSpan.FromSeconds(now - _lastAt), position);
			if (_announce)
			{
				_announce = false;
				_ = SendStateAsync(null, change: true, doSeek: true);
			}
			else if (seeked || paused != _lastPaused)
			{
				_ = SendStateAsync(null, change: true, doSeek: seeked);
			}
		}

		(_lastPosition, _lastPaused, _lastAt) = (position, paused, now);
	}

	private bool OthersOnFile => _file is not null && _people.ContainsValue(_file);

	private void OnPosition(object? sender, TimeSpan position) => Interlocked.Exchange(ref _positionTicks, position.Ticks);

	private Task SendStateAsync(double? latency, bool change, bool doSeek = false)
	{
		var state = new JsonObject();
		// Not while the player's still getting where it's going: the server takes any pause or play that isn't the room's as
		// the user's, so a player still opening (or not yet paused as asked) would set the room going.
		if ((_clientIgnoring == 0 || _serverIgnoring != 0) && _player is { } player && (change || Now >= _settleUntil))
		{
			var play = new JsonObject { ["position"] = Position.TotalSeconds, ["paused"] = player.IsPaused };
			if (doSeek)
			{
				play["doSeek"] = true;
			}

			state["playstate"] = play;
		}
		else if (_player is null && _roomState is var (position, paused, at) && _clientIgnoring == 0)
		{
			// Nothing playing here: the room as it is, so this doesn't hold it back where playback stopped (the server keeps
			// the room with whoever's furthest behind, by where each last said they were).
			state["playstate"] = new JsonObject { ["position"] = paused ? position : position + (Now - at), ["paused"] = paused };
		}

		var ping = new JsonObject { ["clientLatencyCalculation"] = Now, ["clientRtt"] = _rtt };
		if (latency is { } echoed)
		{
			ping["latencyCalculation"] = echoed;
		}

		state["ping"] = ping;
		if (change)
		{
			_clientIgnoring++;
		}

		if (_serverIgnoring != 0 || _clientIgnoring != 0)
		{
			var ignoring = new JsonObject();
			if (_serverIgnoring != 0)
			{
				ignoring["server"] = _serverIgnoring;
				_serverIgnoring = 0;
			}

			if (_clientIgnoring != 0)
			{
				ignoring["client"] = _clientIgnoring;
			}

			state["ignoringOnTheFly"] = ignoring;
		}

		return SendAsync(new JsonObject { ["State"] = state });
	}

	private async Task SendAsync(JsonObject message)
	{
		if (_tcp is not { Connected: true } tcp)
		{
			return;
		}

		// Syncplay's server reads lines ending in CRLF.
		var bytes = Encoding.UTF8.GetBytes(message.ToJsonString() + "\r\n");
		await _writeLock.WaitAsync();
		try
		{
			await tcp.GetStream().WriteAsync(bytes);
		}
		catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
		{
			// Lost: the read loop says so.
		}
		finally
		{
			_writeLock.Release();
		}
	}

	private void ShowPeople()
	{
		Status = _people.Count == 0 ? $"In {_room} as {_name}, waiting for the others" : $"Watching with {string.Join(", ", _people.Keys)} in {_room}";
		// And in the built-in player's list of who's in the room.
		(_player as IInteractiveMediaPlayer)?.SetRoomPeople([new RoomPerson(_name, true, _file ?? ""), .. _people.Select(x => new RoomPerson(x.Key, false, x.Value))]);
	}

	/// <summary>Says something about someone in the room: as a notice over the video in the built-in player, or as a message.</summary>
	private void Notice(string person, string text)
	{
		if (_player is IInteractiveMediaPlayer interactive)
		{
			interactive.ShowRoomNotice(person, text);
		}
		else
		{
			Say($"{person} {text}");
		}
	}

	private void Say(string text) => _ = _player?.ShowMessageAsync(text, s_messageDuration);

	private static string Clock(TimeSpan time) => time.ToString(time.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");

	/// <summary>What to do to the player to be where the room is: pause or resume it (null to leave it), and where to seek (null to stay).</summary>
	internal static (bool? Pause, TimeSpan? SeekTo) Follow(TimeSpan local, bool localPaused, TimeSpan room, bool roomPaused, bool doSeek)
	{
		var apart = (room - local).Duration();
		var seek = apart > (doSeek || roomPaused ? s_seekThreshold : s_desyncThreshold);
		return (roomPaused != localPaused ? roomPaused : null, seek ? room : null);
	}

	/// <summary>Whether the player got to <paramref name="position"/> by seeking, rather than by playing (or staying paused) since the last check.</summary>
	internal static bool IsSeek(TimeSpan last, bool lastPaused, TimeSpan elapsed, TimeSpan position)
	{
		var expected = lastPaused ? last : last + elapsed;
		// Playing, only a jump the room wouldn't count as in step: the built-in player lands a seek on a keyframe a few
		// seconds off, and a stream can stall, neither of which is the user's doing.
		return (position - expected).Duration() > (lastPaused ? s_seekThreshold : s_desyncThreshold);
	}

	/// <summary>Reads "host:port" (or just the host, on Syncplay's usual port 8999).</summary>
	internal static bool TryParseServer(string server, out string host, out int port)
	{
		var parts = server.Trim().Split(':');
		(host, port) = (parts[0], 8999);
		return host.Length > 0 && parts.Length <= 2 && (parts.Length == 1 || int.TryParse(parts[1], out port) && port is > 0 and < 65536);
	}
}
