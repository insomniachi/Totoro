using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Settings;
using Totoro.Helpers;

namespace Totoro.Modules.MediaPlayers.Mpv;

/// <summary>
/// Plays in mpv as a separate process, following its position and duration over mpv's JSON IPC (a named pipe).
/// </summary>
internal sealed class MpvMediaPlayer(IModuleSettings<Settings> settings, ILogger<MpvMediaPlayer> logger) : IMediaPlayer
{
	private readonly SemaphoreSlim _writeLock = new(1, 1);
	private Process? _process;

	// The IPC pipe to the current mpv, once connected; commands sent before then are dropped.
	private NamedPipeClientStream? _pipe;

	// Replies mpv hasn't sent yet, by request id.
	private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _requests = new();
	private int _lastRequest;

	// The current video's subtitle files not yet added; added once mpv has loaded it (sub-add needs a file playing).
	private IReadOnlyList<SubtitleTrack> _subtitles = [];

	// mpv's volume as last reported, kept for the next video once it closes.
	private int? _volume;

	// Play's thread (the UI's): saving settings notifies views, which mpv's pipe thread mustn't do.
	private SynchronizationContext? _ui;

	public Guid Id => MpvModule.Id;

	public string Name => "MPV";

	public event EventHandler<TimeSpan>? DurationChanged;
	public event EventHandler<TimeSpan>? PositionChanged;
	public event EventHandler? PlaybackStopped;

	public bool IsAvailable() => GetFileName() is not null;

	/// <summary>The mpv.exe to play in: the user's own, else the one downloaded from the settings, else one that's installed.</summary>
	private string? GetFileName()
	{
		if (File.Exists(settings.Value.FileName))
		{
			return settings.Value.FileName;
		}

		return MpvInstaller.IsInstalled ? MpvInstaller.Exe : FindMpv();
	}

	public void Play(MediaItem media, TimeSpan startPosition)
	{
		Stop();
		_ui = SynchronizationContext.Current;

		var pipeName = $"totoro-mpv-{Guid.NewGuid():N}";
		var startInfo = new ProcessStartInfo
		{
			FileName = GetFileName() ?? throw new FileNotFoundException("mpv isn't there: download it in the MPV module's settings."),
			ArgumentList =
			{
				media.Uri.IsFile ? media.Uri.LocalPath : media.Uri.AbsoluteUri,
				$"--force-media-title={media.Title}",
				$@"--input-ipc-server=\\.\pipe\{pipeName}",
				$"--start={startPosition.TotalSeconds.ToString(CultureInfo.InvariantCulture)}",
				// Close at the end even when an mpv.conf says keep-open: the exit is what tells the app playback's done.
				"--keep-open=no",
				// Started without a console, mpv's pseudo-gui mode turns idle on, which keeps it open after the video too.
				"--idle=no",
				$"--volume={settings.Value.Volume}",
			}
		};

		if (settings.Value.LaunchFullScreen)
		{
			startInfo.ArgumentList.Add("--fullscreen");
		}

		if (media.Headers is { Count: > 0 } headers)
		{
			startInfo.ArgumentList.Add($"--http-header-fields={string.Join(",", headers.Select(x => $"{x.Key}: {x.Value}"))}");
		}

		_subtitles = media.Subtitles;
		var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
		process.Exited += (_, _) =>
		{
			// Only for the current process: one that was replaced by a newer Play isn't a stop.
			if (ReferenceEquals(process, _process))
			{
				PlaybackStopped?.Invoke(this, EventArgs.Empty);
			}
		};

		process.Start();
		// Ends with the app: without it nothing tracks what's watched, which is the point of playing from here.
		ChildProcesses.Add(process);
		_process = process;
		_ = Task.Run(() => FollowAsync(process, pipeName));
	}

	public Task SeekAsync(TimeSpan position)
	{
		return SendAsync("seek", position.TotalSeconds, "absolute+exact");
	}

	public Task ShowMessageAsync(string text, TimeSpan duration)
	{
		return SendAsync("show-text", text, (int)duration.TotalMilliseconds);
	}

	public Task SetChaptersAsync(IReadOnlyList<MediaChapter> chapters)
	{
		// The chapter-list property can be replaced while playing; it shows on the seek bar, and the next/previous
		// chapter keys (Page Up/Down) move between them.
		object[] list = [.. chapters.Select(x => new { title = x.Title, time = x.Start.TotalSeconds })];
		return SendAsync("set_property", "chapter-list", list);
	}

	public async Task<IReadOnlyList<MediaChapter>> GetChaptersAsync()
	{
		var id = Interlocked.Increment(ref _lastRequest);
		var reply = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
		_requests[id] = reply;
		try
		{
			await SendMessageAsync(new { command = new object[] { "get_property", "chapter-list" }, request_id = id });
			var data = await reply.Task.WaitAsync(TimeSpan.FromSeconds(2));
			return data.ValueKind != JsonValueKind.Array
				? []
				:
				[
					.. data.EnumerateArray().Select(x => new MediaChapter(
						x.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
						TimeSpan.FromSeconds(x.GetProperty("time").GetDouble())))
				];
		}
		catch (TimeoutException)
		{
			// Not connected, or mpv was closed meanwhile.
			return [];
		}
		finally
		{
			_requests.TryRemove(id, out _);
		}
	}

	private Task SendAsync(params object[] command) => SendMessageAsync(new { command });

	private async Task SendMessageAsync(object message)
	{
		if (_pipe is not { IsConnected: true } pipe)
		{
			return;
		}

		await _writeLock.WaitAsync();
		try
		{
			await WriteAsync(pipe, message);
		}
		catch (Exception ex) when (ex is IOException or ObjectDisposedException)
		{
			// mpv was closed meanwhile.
		}
		finally
		{
			_writeLock.Release();
		}
	}

	private void Stop()
	{
		if (_process is not { } process)
		{
			return;
		}

		_process = null;
		_pipe = null;
		try
		{
			if (!process.HasExited)
			{
				process.Kill();
			}
		}
		catch (InvalidOperationException)
		{
			// Already gone.
		}
	}

	/// <summary>Connects to mpv's IPC pipe and reports position and duration until mpv exits.</summary>
	private async Task FollowAsync(Process process, string pipeName)
	{
		try
		{
			await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

			// mpv creates the pipe once it's started; keep trying until then.
			while (!pipe.IsConnected)
			{
				if (process.HasExited)
				{
					return;
				}

				try
				{
					await pipe.ConnectAsync(500);
				}
				catch (TimeoutException)
				{
				}
				catch (IOException)
				{
					await Task.Delay(250);
				}
			}

			await WriteAsync(pipe, new { command = new object[] { "observe_property", 1, "duration" } });
			await WriteAsync(pipe, new { command = new object[] { "observe_property", 2, "time-pos" } });
			await WriteAsync(pipe, new { command = new object[] { "observe_property", 3, "track-list" } });
			await WriteAsync(pipe, new { command = new object[] { "observe_property", 4, "volume" } });
			if (ReferenceEquals(process, _process))
			{
				_pipe = pipe;
			}

			using var reader = new StreamReader(pipe, Encoding.UTF8);
			while (await reader.ReadLineAsync() is { } line)
			{
				Handle(line);
			}
		}
		catch (Exception ex) when (ex is IOException or ObjectDisposedException)
		{
			// mpv closed the pipe on exit.
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Lost track of mpv");
		}
		finally
		{
			if (_volume is { } volume && volume != settings.Value.Volume)
			{
				settings.Value.Volume = volume;
				if (_ui is { } ui)
				{
					ui.Post(_ => settings.Save(), null);
				}
				else
				{
					settings.Save();
				}
			}
		}
	}

	private void Handle(string line)
	{
		using var message = JsonDocument.Parse(line);
		var root = message.RootElement;
		if (root.TryGetProperty("request_id", out var request) && _requests.TryRemove(request.GetInt32(), out var reply))
		{
			reply.TrySetResult(root.TryGetProperty("data", out var result) ? result.Clone() : default);
			return;
		}

		// The track list has tracks once the video's loaded (observing it reports it as it is, so a video loaded before the
		// pipe connected counts too): then its subtitle files go in, once.
		if (root.TryGetProperty("name", out var property) && property.GetString() == "track-list" &&
			root.TryGetProperty("data", out var tracks) && tracks.ValueKind == JsonValueKind.Array && tracks.GetArrayLength() > 0)
		{
			// With their names and languages, which --sub-file can't give; the first is shown.
			foreach (var (subtitle, i) in Interlocked.Exchange(ref _subtitles, []).Select((x, i) => (x, i)))
			{
				var url = subtitle.Url.IsFile ? subtitle.Url.LocalPath : subtitle.Url.AbsoluteUri;
				_ = SendAsync("sub-add", url, i == 0 ? "select" : "auto", subtitle.Name, subtitle.Language ?? "");
			}

			return;
		}

		if (!root.TryGetProperty("event", out var type) || type.GetString() != "property-change" ||
			!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Number)
		{
			return;
		}

		var value = TimeSpan.FromSeconds(data.GetDouble());
		switch (root.GetProperty("name").GetString())
		{
			case "volume":
				_volume = (int)Math.Round(data.GetDouble());
				break;
			case "duration":
				DurationChanged?.Invoke(this, value);
				break;
			case "time-pos":
				PositionChanged?.Invoke(this, value);
				break;
		}
	}

	private static async Task WriteAsync(Stream pipe, object message)
	{
		var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message) + "\n");
		await pipe.WriteAsync(bytes);
		await pipe.FlushAsync();
	}

	/// <summary>mpv's installed location: where its installer registers it, or on the PATH.</summary>
	private static string? FindMpv()
	{
		foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
		{
			using var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\mpv.exe");
			if (key?.GetValue("") is string path && File.Exists(path))
			{
				return path;
			}
		}

		return (Environment.GetEnvironmentVariable("PATH") ?? "")
			   .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
			   .Select(x => Path.Combine(x.Trim('"'), "mpv.exe"))
			   .FirstOrDefault(File.Exists);
	}
}
