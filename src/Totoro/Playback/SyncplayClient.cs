using System.Diagnostics;
using System.Net.Sockets;
using System.Collections.ObjectModel;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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

	// The server says where the room is about once a second: this long without a word, the connection's dead even if the
	// socket hasn't said so (Wi-Fi changed, the PC slept, a router dropped it).
	private const double SilenceSeconds = 10;

	// Lost after being in the room, it tries again after each of these in turn, then gives up.
	private static readonly TimeSpan[] s_reconnectDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

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
	// What's said and heard over it: the TCP stream, or the encryption over it.
	private Stream? _stream;
	private string _password = "";
	private CancellationTokenSource? _cts;
	// The current connection's, cancelled when the server's gone quiet (see Tick); and when it last heard from it.
	private CancellationTokenSource? _connection;
	private double _lastHeard;
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

	// Whether the episode playing here has caught up with the room since it started (see OnState); always when leading it.
	private bool _caughtUp;

	// The episode last started following someone (see PlayAsync), which never sets the room going (see Tick).
	private (AnimeModel Anime, int Episode)? _following;

	// When the episode playing here started (Unix ms, sent with it): of two started at once, the later is followed.
	private long _startedAt;

	// Couldn't follow the episode someone started: what plays here is another, kept out of the room's pauses and seeks.
	private bool _offRoom;

	// Whether the server has said who's in the room (asked for on joining): till then, what plays here is left alone,
	// or it would seem nobody else is on it and set the room going (see Tick).
	private bool _listed;

	// Whether the player's stuck loading, which the room's told is a pause (see Tick); and when it last moved on.
	private bool _stalled;
	private double _advancedAt;

	// Playing and not moving this long is loading, not the user's doing.
	private const double StallSeconds = 2;

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

	/// <summary>The server's password, for a private one; empty for none.</summary>
	public string Password
	{
		get => _preferences.SyncplayPassword;
		set => Save(Password, value, x => _preferences.SyncplayPassword = x);
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
		// And again when the app next starts, until the user leaves (see Leave).
		_preferences.SyncplayRejoin = true;
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

	/// <summary>Leaves the room, for good: the app doesn't join it again as it next starts.</summary>
	public void Leave()
	{
		_preferences.SyncplayRejoin = false;
		IsOn = false;
	}

	/// <summary>Joins the room again if the user was in it when the app last closed.</summary>
	public void Rejoin()
	{
		if (_preferences.SyncplayRejoin)
		{
			_ = JoinAsync();
		}
	}

	/// <summary>Says <paramref name="text"/> to the room (cut to Syncplay's length); false when not in one.</summary>
	public bool SendChat(string text)
	{
		if (!_listed || text.Trim() is not { Length: > 0 } said)
		{
			return false;
		}

		_ = TellAsync(said);
		// The server sends it back to everyone, this app too, which ignores its own (see Handle): kept here instead.
		Remember($"{_name} (you): {FitChat(said)}");
		return true;
	}

	private void OnRoomChat(object? sender, string text) => SendChat(text);

	// How much of what the room said is kept to show.
	private const int MessagesKept = 30;

	/// <summary>What's been said in the room and who came and went, the latest last; for while nothing's playing (the player shows it over the video).</summary>
	public ObservableCollection<string> Messages { get; } = [];

	private void Remember(string message)
	{
		Messages.Add(message);
		while (Messages.Count > MessagesKept)
		{
			Messages.RemoveAt(0);
		}
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
		_password = Password;
		if (!TryParseServer(Server, out var host, out var port) || _name.Length == 0 || _room.Length == 0)
		{
			Disconnect("Fill in the server (host:port), the room and your name first.");
			return;
		}

		var cts = _cts = new CancellationTokenSource();
		Status = $"Joining {_room}…";
		var (everJoined, failures) = (false, 0);
		while (true)
		{
			var (joined, error) = await ConnectOnceAsync(host, port, cts.Token);
			if (cts.IsCancellationRequested)
			{
				// Left the room, or the server turned it down (which says why).
				return;
			}

			everJoined |= joined;
			failures = joined ? 0 : failures + 1;
			if (ReconnectDelay(everJoined, failures) is not { } delay)
			{
				Disconnect(error);
				return;
			}

			TearDown();
			Status = $"{error.TrimEnd('.')}. Reconnecting…";
			try
			{
				await Task.Delay(delay, cts.Token);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			Status = $"Rejoining {_room}…";
		}
	}

	/// <summary>
	/// How long to wait before trying again, after a connection's <paramref name="failures"/>th failure in a row (0: it was
	/// in the room, then lost it); null to give up. Never in the room, it's the server, name or room that's wrong: not retried.
	/// </summary>
	internal static TimeSpan? ReconnectDelay(bool everJoined, int failures) =>
		everJoined && failures < s_reconnectDelays.Length ? s_reconnectDelays[failures] : null;

	/// <summary>Joins over a new connection and follows the room until it's lost: whether it got in, and why it was lost.</summary>
	private async Task<(bool Joined, string Error)> ConnectOnceAsync(string host, int port, CancellationToken ct)
	{
		using var connection = _connection = CancellationTokenSource.CreateLinkedTokenSource(ct);
		var joined = false;
		try
		{
			// Ticking from here: a server that never answers is let go after a while (see Tick), not waited on for ever.
			_lastHeard = Now;
			_timer.Start();
			var stream = await OpenAsync(host, port, connection.Token);
			var hello = new JsonObject
			{
				["username"] = _name,
				["room"] = new JsonObject { ["name"] = _room },
				["version"] = ProtocolVersion,
				["realversion"] = ReleaseVersion,
				["features"] = new JsonObject { ["sharedPlaylists"] = false, ["chat"] = true, ["readiness"] = true, ["managedRooms"] = false },
			};
			if (_password.Length > 0)
			{
				// As Syncplay's app sends it: the server keeps its password hashed so.
				hello["password"] = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(_password)));
			}

			await SendAsync(new JsonObject { ["Hello"] = hello });
			using var reader = new StreamReader(stream, Encoding.UTF8);
			while (await reader.ReadLineAsync(connection.Token) is { } line)
			{
				_lastHeard = Now;
				var message = JsonNode.Parse(line);
				joined |= message?["Hello"] is not null;
				Handle(message);
			}

			return (joined, "The server closed the connection.");
		}
		catch (Exception) when (ct.IsCancellationRequested)
		{
			// Left the room.
			return (joined, "");
		}
		catch (Exception ex) when (connection.IsCancellationRequested)
		{
			_logger.LogWarning(ex, "Heard nothing from the Syncplay server");
			return (joined, $"Heard nothing from {host} for {SilenceSeconds:0} seconds.");
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Lost the Syncplay server");
			return (joined, $"Lost {host}: {ex.Message}");
		}
		finally
		{
			// Unless rejoining (JoinAsync) has made another meanwhile.
			if (ReferenceEquals(_connection, connection))
			{
				_connection = null;
			}
		}
	}

	/// <summary>
	/// Connects, encrypted where the server can be (syncplay.pl's can): it's asked first, and turns it on or says it can't.
	/// A server from before Syncplay had encryption turns the question down and closes: then it's asked again without it.
	/// </summary>
	private async Task<Stream> OpenAsync(string host, int port, CancellationToken ct)
	{
		var tcp = _tcp = new TcpClient();
		await tcp.ConnectAsync(host, port, ct);
		Stream stream = tcp.GetStream();
		await WriteLineAsync(stream, new JsonObject { ["TLS"] = new JsonObject { ["startTLS"] = "send" } }, ct);
		var reply = await ReadLineAsync(stream, ct);
		var answer = reply is null ? null : (string?)JsonNode.Parse(reply)?["TLS"]?["startTLS"];
		if (answer == "true")
		{
			var tls = new SslStream(stream);
			// Checked as any HTTPS site's is: a server whose certificate isn't valid isn't joined.
			await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, ct);
			IsEncrypted = true;
			return _stream = tls;
		}

		if (answer is null)
		{
			tcp.Dispose();
			tcp = _tcp = new TcpClient();
			await tcp.ConnectAsync(host, port, ct);
			stream = tcp.GetStream();
		}

		IsEncrypted = false;
		return _stream = stream;
	}

	/// <summary>Whether the connection to the server is encrypted, so the room, chat and what's watched aren't sent in the open.</summary>
	[ObservableProperty]
	public partial bool IsEncrypted { get; private set; }

	/// <summary>A line from <paramref name="stream"/> a byte at a time: before encryption starts, nothing after it may be read.</summary>
	private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken ct)
	{
		var line = new List<byte>();
		var b = new byte[1];
		while (await stream.ReadAsync(b, ct) == 1)
		{
			if (b[0] == '\n')
			{
				return Encoding.UTF8.GetString([.. line]).TrimEnd('\r');
			}

			line.Add(b[0]);
		}

		return null;
	}

	// Syncplay's server reads lines ending in CRLF.
	private static Task WriteLineAsync(Stream stream, JsonObject message, CancellationToken ct = default) =>
		stream.WriteAsync(Encoding.UTF8.GetBytes(message.ToJsonString() + "\r\n"), ct).AsTask();

	private void Disconnect(string status)
	{
		_cts?.Cancel();
		_cts = null;
		TearDown();
		// Comes back here with no status when it was on: the one given is said after.
		IsOn = false;
		Status = status;
		IsRoomMessageOpen = false;
		IsEncrypted = false;
		Messages.Clear();
		UpdateCatchUp();
		_joining?.TrySetResult();
	}

	/// <summary>Drops the connection and everything that came with it: on leaving the room, or before rejoining it.</summary>
	private void TearDown()
	{
		_stream?.Dispose();
		_tcp?.Dispose();
		(_stream, _tcp) = (null, null);
		if (_slowed)
		{
			// Back to normal speed, out of the room.
			_ = _player?.SetSpeedAsync(1);
			_slowed = false;
		}

		if (_player is IInteractiveMediaPlayer chatting)
		{
			chatting.RoomChatSent -= OnRoomChat;
		}

		_timer.Stop();
		_people.Clear();
		_episodes.Clear();
		if (_player is not null)
		{
			_player.PositionChanged -= OnPosition;
			(_player as IInteractiveMediaPlayer)?.SetRoomPeople(null);
		}

		(_player, _file, _fileSent, _roomState) = (null, null, null, null);
		(_clientIgnoring, _serverIgnoring, _offRoom, _listed, _stalled, _rtt) = (0, 0, false, false, false, 0);
	}

	private void Handle(JsonNode? message)
	{
		if (message?["Hello"] is JsonObject hello)
		{
			// The server changes the name if someone in the room has it already.
			_name = (string?)hello["username"] ?? _name;
			// And cuts a long room name (to 35 characters): the room is the one it says.
			_room = (string?)hello["room"]?["name"] ?? _room;
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
			_listed = true;
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
			Notice(from, said.StartsWith(CouldNotPlay, StringComparison.Ordinal) || said == Loading ? said : $"says “{said}”");
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
				_ = PlayAsync(name, (string?)episode["service"], (long?)episode["id"] ?? 0, (int?)episode["episode"] ?? 0, (long?)episode["startedAt"] ?? 0);
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
	/// <param name="startedAt">When they started it (Unix ms; 0 from a Totoro that doesn't say).</param>
	/// <param name="asked">Whether the user asked for it, rather than following someone (see <see cref="ShouldFollow"/>).</param>
	private async Task PlayAsync(string by, string? service, long id, int episode, long startedAt = 0, bool asked = false)
	{
		if (service is null || id <= 0 || episode <= 0 || !asked && !ShouldFollow(by, startedAt))
		{
			return;
		}

		if (_watch.IsActive && _watch.Anime is { } playing && playing.ServiceName == service && playing.Id == id && _watch.CurrentEpisode == episode)
		{
			if (_offRoom)
			{
				// Out of step since a follow failed, and the room's back on what plays here: in step again, catching up first.
				(_offRoom, _caughtUp) = (false, false);
				Say($"{by} is back on this episode: in step with the room");
			}

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
				LeaveStep();
				_ = TellAsync($"{CouldNotPlay}episode {episode}: it isn't on {database.Name}");
				return;
			}

			var anime = await database.GetAnimeAsync(ownId.Value);
			_following = (anime, episode);
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
			LeaveStep();
			_ = TellAsync($"{CouldNotPlay}episode {episode}: {ex.Message}");
		}
	}

	/// <summary>
	/// Whether to follow an episode someone started (see <see cref="PlayAsync"/>): always, unless one started here a moment
	/// ago; then only the later of the two (see <see cref="StartedAfter"/>), so two started at once end up one, not swapped.
	/// </summary>
	private bool ShouldFollow(string by, long startedAt) =>
		_player is null || Now >= _followEpisodesAfter || StartedAfter(startedAt, by, _startedAt, _name);

	/// <summary>
	/// Whether their start (<paramref name="theirs"/>, by <paramref name="by"/>) came after ours, the name settling a tie: each
	/// side compares the same two, so exactly one follows the other, however far apart their clocks. Unknown (0) never is.
	/// </summary>
	internal static bool StartedAfter(long theirs, string by, long ours, string me) =>
		theirs > 0 && (theirs > ours || theirs == ours && string.CompareOrdinal(by, me) < 0);

	/// <summary>What's playing here isn't what someone started: it's kept out of the room until another episode plays.</summary>
	private void LeaveStep()
	{
		if (_player is not null)
		{
			_offRoom = true;
			Say($"{Status} Not in step with the room.");
		}
	}

	// What a chat message starts with when Totoro couldn't play an episode someone started; shown as it is, after the name.
	private const string CouldNotPlay = "couldn't play ";

	// The chat message saying the room's paused for someone's player loading (see Tick); shown as it is too.
	private const string Loading = "is loading: the room waits";

	/// <summary>
	/// Whether the player's stuck loading: playing, yet not moved on for <paramref name="stillFor"/> seconds. Not near
	/// the end, where a player can sit before it says it's done.
	/// </summary>
	internal static bool IsStalled(bool paused, double stillFor, TimeSpan position, TimeSpan duration) =>
		!paused && stillFor > StallSeconds && duration - position > TimeSpan.FromSeconds(5);

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

		// Till an episode that started here catches up, the room is followed even as set by this app: the server says so of
		// whoever's furthest behind, which may be this app as it was (saying the room back) before the episode opened.
		if (state["playstate"] is JsonObject play && (string?)play["setBy"] is var by && (by != _name || !_caughtUp) &&
			_clientIgnoring == 0 && _player is { } player && Now >= _settleUntil && !_announce && !_offRoom)
		{
			var paused = (bool?)play["paused"] ?? true;
			var doSeek = (bool?)play["doSeek"] ?? false;
			// shortcut: takes half the round trip as how old the message is, where Syncplay averages; fine on a steady connection.
			var room = TimeSpan.FromSeconds(Math.Max(0, ((double?)play["position"] ?? 0) + (paused ? 0 : _rtt / 2)));
			var (pause, seekTo) = Follow(Position, player.IsPaused, room, paused, doSeek);
			_caughtUp = true;
			if (_stalled && pause == true)
			{
				// The room's paused for this player loading: pausing it too would leave it paused once loaded.
				pause = null;
			}

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
				if (by is { Length: > 0 } && by != _name)
				{
					Say(pause is { } nowPaused ? $"{by} {(nowPaused ? "paused" : "resumed")}" : doSeek ? $"{by} jumped to {Clock(room)}" : $"Caught up with {by}");
				}
			}

			// A little ahead of the room (someone a little behind): a little slower till they're level, as Syncplay's app
			// does, rather than drifting till it's far enough to jump back. Normal again on any pause or jump.
			var still = paused || player.IsPaused || pause is not null || seekTo is not null;
			if ((still ? _slowed ? 1 : null : Slowdown((Position - room).TotalSeconds, _slowed)) is { } speed)
			{
				_ = player.SetSpeedAsync(speed);
				_slowed = speed < 1;
			}
		}

		if (_slowed && (string?)state["playstate"]?["setBy"] == _name)
		{
			// The room's where this player is now (it's the furthest behind): level, so normal speed again.
			_ = _player?.SetSpeedAsync(1);
			_slowed = false;
		}

		_ = SendStateAsync(latency, change: false);
	}

	/// <summary>Follows the player playing (a new episode starts over), and says what the user did in it.</summary>
	private void Tick()
	{
		if (Now - _lastHeard > SilenceSeconds)
		{
			// Gone quiet: lost, though the socket hasn't said so (see ConnectOnceAsync).
			_connection?.Cancel();
			return;
		}

		UpdateCatchUp();
		if (!_listed)
		{
			return;
		}

		var player = _watch.PlayingPlayer;
		var file = player is null ? null : _watch.Title;
		if (!ReferenceEquals(player, _player) || file != _file)
		{
			if (_player is not null)
			{
				_player.PositionChanged -= OnPosition;
				(_player as IInteractiveMediaPlayer)?.RoomChatSent -= OnRoomChat;
				if (player is null)
				{
					// Closed (or finished) while in the room: no longer watching, which the others see.
					_fileSent = null;
					_ = SendAsync(new JsonObject { ["Set"] = new JsonObject { ["file"] = new JsonObject { ["name"] = "", ["duration"] = 0, ["size"] = 0 } } });
				}
			}

			(_player, _file, _offRoom, _stalled, _advancedAt) = (player, file, false, false, Now);
			if (player is not null)
			{
				Interlocked.Exchange(ref _positionTicks, -1);
				player.PositionChanged += OnPosition;
				(player as IInteractiveMediaPlayer)?.RoomChatSent += OnRoomChat;
				// A new episode plays at normal speed.
				_slowed = false;
				// Never when it was started following someone: they're on it, whatever their title for it.
				_announce = !OthersOnFile && !(_following is var (anime, number) && ReferenceEquals(anime, _watch.Anime) && number == _watch.CurrentEpisode);
				// Leading, it's where the room is; otherwise it's to catch up first (see OnState).
				_caughtUp = _announce;
				_followEpisodesAfter = Now + FollowEpisodesAfterSeconds;
				_startedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
				sent["totoro"] = new JsonObject { ["service"] = service, ["id"] = anime.Id, ["episode"] = _watch.CurrentEpisode, ["startedAt"] = _startedAt };
			}

			_ = SendAsync(new JsonObject { ["Set"] = new JsonObject { ["file"] = sent } });
			_ = SendAsync(new JsonObject { ["Set"] = new JsonObject { ["ready"] = new JsonObject { ["isReady"] = true, ["manuallyInitiated"] = false } } });
		}

		if (paused || position != _lastPosition || Interlocked.Read(ref _positionTicks) < 0)
		{
			_advancedAt = now;
		}

		// Loading (a stream or torrent catching up): said as paused, so the room waits for it rather than the server
		// taking everyone back to it again and again (it keeps the room with whoever's furthest behind).
		var stalled = now >= _settleUntil && !_offRoom && _caughtUp && IsStalled(paused, now - _advancedAt, position, _watch.PlaybackDuration);
		var stallChanged = stalled != _stalled;
		_stalled = stalled;
		if (stallChanged && stalled)
		{
			Say("Loading: the room waits for you");
			_ = TellAsync(Loading);
		}

		if (now >= _settleUntil && !_offRoom && _caughtUp)
		{
			var seeked = IsSeek(_lastPosition, _lastPaused, TimeSpan.FromSeconds(now - _lastAt), position);
			if (_announce)
			{
				_announce = false;
				_ = SendStateAsync(null, change: true, doSeek: true);
			}
			else if (seeked || paused != _lastPaused || stallChanged)
			{
				_ = SendStateAsync(null, change: true, doSeek: seeked);
			}
		}

		(_lastPosition, _lastPaused, _lastAt) = (position, paused, now);
	}

	// By the title, or (titles differ with the database and its title language) by the episode, where the database is the same.
	private bool OthersOnFile => _file is not null && (_people.ContainsValue(_file) || _watch.Anime is { } anime && _episodes.Values.Any(x =>
		(string?)x.Episode["service"] == anime.ServiceName && (long?)x.Episode["id"] == anime.Id && (int?)x.Episode["episode"] == _watch.CurrentEpisode));

	private void OnPosition(object? sender, TimeSpan position) => Interlocked.Exchange(ref _positionTicks, position.Ticks);

	private Task SendStateAsync(double? latency, bool change, bool doSeek = false)
	{
		var state = new JsonObject();
		// Out of step (another episode than the room's), it says the room's state back, as with nothing playing.
		var current = _offRoom ? null : _player;
		// Not while the player's still getting where it's going: the server takes any pause or play that isn't the room's as
		// the user's, so a player still opening (or not yet paused as asked) would set the room going.
		// Nor before it's caught up with the room: where it opened isn't where the room is.
		if ((_clientIgnoring == 0 || _serverIgnoring != 0) && current is { } player && (change || Now >= _settleUntil && _caughtUp))
		{
			var play = new JsonObject { ["position"] = Position.TotalSeconds, ["paused"] = player.IsPaused || _stalled };
			if (doSeek)
			{
				play["doSeek"] = true;
			}

			state["playstate"] = play;
		}
		else if (current is null && _roomState is var (position, paused, at) && _clientIgnoring == 0)
		{
			// Nothing playing here: the room as it is, so this doesn't hold it back where playback stopped (the server keeps
			// the room with whoever's furthest behind, by where each last said they were).
			// Playing, with the time the room's state took to get here (as when following it, see OnState): without it, what's
			// said back falls behind a little each second, and as the furthest behind it takes the others back every minute or so.
			state["playstate"] = new JsonObject { ["position"] = paused ? position : position + (Now - at) + _rtt / 2, ["paused"] = paused };
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
		if (_stream is not { } stream)
		{
			return;
		}

		await _writeLock.WaitAsync();
		try
		{
			await WriteLineAsync(stream, message);
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
		Status = (_people.Count == 0 ? $"In {_room} as {_name}, waiting for the others" : $"Watching with {string.Join(", ", _people.Keys)} in {_room}") +
				 (IsEncrypted ? "" : " (not encrypted: this server can't)");
		// And in the built-in player's list of who's in the room.
		(_player as IInteractiveMediaPlayer)?.SetRoomPeople([new RoomPerson(_name, true, _file ?? ""), .. _people.Select(x => new RoomPerson(x.Key, false, x.Value))]);
	}

	/// <summary>
	/// The latest about someone in the room (chat, someone joining) while nothing's playing here, for the main window to
	/// show: the player would otherwise, and with none it'd be lost. Stays till it's closed or the next comes.
	/// </summary>
	[ObservableProperty]
	public partial string RoomMessage { get; private set; } = "";

	[ObservableProperty]
	public partial bool IsRoomMessageOpen { get; set; }

	/// <summary>Says something about someone in the room: as a notice over the video in the built-in player, as a message in another, or in the main window.</summary>
	private void Notice(string person, string text)
	{
		Remember($"{person} {text}");
		if (_player is IInteractiveMediaPlayer interactive)
		{
			interactive.ShowRoomNotice(person, text);
		}
		else if (_player is not null)
		{
			Say($"{person} {text}");
		}
		else
		{
			RoomMessage = $"{person} {text}";
			IsRoomMessageOpen = true;
		}
	}

	private void Say(string text) => _ = _player?.ShowMessageAsync(text, s_messageDuration);

	private static string Clock(TimeSpan time) => time.ToString(time.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");

	// Syncplay's own: slower by this much from this far ahead of the room (seconds), till this close.
	private const double SlowdownRate = 0.95;
	private const double SlowdownAfter = 1.5;
	private const double SlowdownUntil = 0.1;

	// Whether the player's slowed down (see Slowdown).
	private bool _slowed;

	/// <summary>The speed to play at, <paramref name="ahead"/> seconds ahead of the room (null to keep it).</summary>
	internal static double? Slowdown(double ahead, bool slowed) =>
		!slowed && ahead > SlowdownAfter ? SlowdownRate : slowed && ahead < SlowdownUntil ? 1 : null;

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
