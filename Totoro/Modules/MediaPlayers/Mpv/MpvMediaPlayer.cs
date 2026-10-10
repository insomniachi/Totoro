using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Totoro.Core.Module.Media;
using Totoro.Core.Settings;

namespace Totoro.Modules.MediaPlayers.Mpv;

/// <summary>
/// Plays in mpv as a separate process, following its position and duration over mpv's JSON IPC (a named pipe).
/// </summary>
internal sealed class MpvMediaPlayer(IModuleSettings<Settings> settings, ILogger<MpvMediaPlayer> logger) : IMediaPlayer
{
	private Process? _process;

	public Guid Id => MpvModule.Id;

	public string Name => "MPV";

	public event EventHandler<TimeSpan>? DurationChanged;
	public event EventHandler<TimeSpan>? PositionChanged;
	public event EventHandler? PlaybackStopped;

	public bool IsAvailable()
	{
		if (!File.Exists(settings.Value.FileName) && FindMpv() is { } found)
		{
			settings.Value.FileName = found;
			settings.Save();
		}

		return File.Exists(settings.Value.FileName);
	}

	public void Play(MediaItem media, TimeSpan startPosition)
	{
		Stop();

		var pipeName = $"totoro-mpv-{Guid.NewGuid():N}";
		var startInfo = new ProcessStartInfo
		{
			FileName = settings.Value.FileName,
			ArgumentList =
			{
				media.Uri.IsFile ? media.Uri.LocalPath : media.Uri.AbsoluteUri,
				$"--force-media-title={media.Title}",
				$@"--input-ipc-server=\\.\pipe\{pipeName}",
				$"--start={startPosition.TotalSeconds.ToString(CultureInfo.InvariantCulture)}",
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
		_process = process;
		_ = Task.Run(() => FollowAsync(process, pipeName));
	}

	private void Stop()
	{
		if (_process is not { } process)
		{
			return;
		}

		_process = null;
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

			await SendAsync(pipe, "observe_property", 1, "duration");
			await SendAsync(pipe, "observe_property", 2, "time-pos");

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
	}

	private void Handle(string line)
	{
		using var message = JsonDocument.Parse(line);
		var root = message.RootElement;
		if (!root.TryGetProperty("event", out var type) || type.GetString() != "property-change" ||
			!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Number)
		{
			return;
		}

		var value = TimeSpan.FromSeconds(data.GetDouble());
		switch (root.GetProperty("name").GetString())
		{
			case "duration":
				DurationChanged?.Invoke(this, value);
				break;
			case "time-pos":
				PositionChanged?.Invoke(this, value);
				break;
		}
	}

	private static async Task SendAsync(Stream pipe, params object[] command)
	{
		var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { command }) + "\n");
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
