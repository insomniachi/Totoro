using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Totoro.Core.Modules.Media;
using Totoro.Modules.MediaPlayers.Mpv;
using static Totoro.Modules.MediaPlayers.Mpv.LibMpvNative;
using MediaTrack = Totoro.Core.Modules.Media.MediaTrack;

namespace Totoro.ViewModels.Player;

/// <summary>
/// The player's window, playing with libmpv. mpv draws in a window of its own inside the player's, under the XAML (which
/// is see-through over the video), the way mpv always draws, so mpv's config applies: Anime4K's shaders too.
/// </summary>
public sealed partial class MpvPlayerViewModel(int volume, PlayerAspectRatio aspectRatio, ILogger logger) : PlayerViewModel(volume, aspectRatio, logger)
{
	// 0 until InitializeAsync, and again once closing.
	private readonly ILogger _logger = logger;
	private nint _mpv;
	private Task? _events;
	private nint _window;
	private volatile bool _paused;
	private volatile bool _loaded;
	private volatile bool _atEnd;
	private double _reported = -1;
	private string _subPos = "100";

	// Anime4K's modes, as mpv's input.conf binds them to keys (see MpvInstaller), which don't reach mpv here.
	private readonly IReadOnlyList<ShaderMode> _modes = ReadShaderModes(Path.Combine(MpvInstaller.ConfigFolder, "input.conf"));

	/// <param name="Label">What mpv shows as it's picked, e.g. "Anime4K: Mode A (HQ)".</param>
	/// <param name="Shaders">mpv's glsl-shaders, separated by semicolons.</param>
	internal sealed record ShaderMode(string Label, string Shaders)
	{
		/// <summary>Its name in the menu, e.g. "Mode A (HQ)".</summary>
		public string Name => Label.StartsWith("Anime4K:", StringComparison.OrdinalIgnoreCase) ? Label["Anime4K:".Length..].Trim() : Label;
	}

	/// <summary>
	/// Creates mpv to draw in <paramref name="window"/> (the player's). Off the UI thread: loading libmpv and starting it
	/// takes a moment.
	/// </summary>
	public async Task InitializeAsync(nint window)
	{
		if (_window != 0)
		{
			return;
		}

		_window = window;
		nint started;
		try
		{
			started = await Task.Run(() =>
			{
				LibMpvNative.Load();
				var mpv = Create();
				SetOptionString(mpv, "wid", window.ToString(CultureInfo.InvariantCulture));
				// mpv's own config, where Anime4K's lines are installed (see MpvInstaller), but not the user's scripts: one
				// drawing its own controls would show over the app's.
				SetOptionString(mpv, "config", "yes");
				SetOptionString(mpv, "config-dir", MpvInstaller.ConfigFolder);
				SetOptionString(mpv, "load-scripts", "no");
				SetOptionString(mpv, "vo", "gpu-next");
				SetOptionString(mpv, "hwdec", "auto-safe");
				// A flip-model swap chain is shown over the XAML whatever the windows' order; blitted, it's under it.
				SetOptionString(mpv, "d3d11-flip", "no");
				// Its (black) window straight away: the player's window is see-through where the video goes.
				SetOptionString(mpv, "force-window", "yes");
				SetOptionString(mpv, "volume",((int)Volume).ToString(CultureInfo.InvariantCulture));
				if (Initialize(mpv) < 0)
				{
					TerminateDestroy(mpv);
					throw new InvalidOperationException("mpv didn't start.");
				}

				// After the config, which mustn't change them: the app's controls and keys are the ones used, and the last
				// frame stays at the end (eof-reached says it ended) rather than mpv going idle.
				SetPropertyString(mpv, "osc", "no");
				SetPropertyString(mpv, "input-default-bindings", "no");
				SetPropertyString(mpv, "input-vo-keyboard", "no");
				SetPropertyString(mpv, "input-cursor", "no");
				SetPropertyString(mpv, "cursor-autohide", "no");
				SetPropertyString(mpv, "keep-open", "yes");
				SetPropertyString(mpv, "idle", "yes");
				SetPropertyString(mpv, "sub-pos", _subPos);

				ObserveProperty(mpv, "time-pos", FormatDouble);
				ObserveProperty(mpv, "duration", FormatDouble);
				ObserveProperty(mpv, "pause", FormatFlag);
				ObserveProperty(mpv, "paused-for-cache", FormatFlag);
				ObserveProperty(mpv, "eof-reached", FormatFlag);
				ObserveProperty(mpv, "osd-height", FormatDouble);
				return mpv;
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Couldn't start mpv");
			ShowStatus("Couldn't start mpv: download it again in the MPV player's settings.", isError: true);
			// Plays waiting on it go on (and do nothing, there being no mpv) rather than waiting for ever.
			Initialized.TrySetResult();
			return;
		}

		// Closed while mpv started: closing found no mpv to end, so it's ended here.
		if (IsClosed)
		{
			await Task.Run(() => TerminateDestroy(started));
			Initialized.TrySetResult();
			return;
		}

		_mpv = started;
		MoveBehindXaml(window);
		_events = Task.Run(() => FollowEvents(started));
		ApplyAspectRatio();
		Initialized.TrySetResult();
	}

	protected override Task PlayCoreAsync(Uri uri, IEnumerable<string> options, TimeSpan start)
	{
		var mpv = _mpv;
		if (mpv == 0)
		{
			return Task.CompletedTask;
		}

		_atEnd = false;
		_reported = -1;
		// Per-file options (they last for this video only), each value length-prefixed (%n%) so commas in it are kept.
		var fileOptions = string.Join(",", options.Append($"start={start.TotalSeconds.ToString(CultureInfo.InvariantCulture)}").Select(x =>
		{
			var (name, value) = (x[..x.IndexOf('=')], x[(x.IndexOf('=') + 1)..]);
			return $"{name}=%{Encoding.UTF8.GetByteCount(value)}%{value}";
		}));
		var url = uri.IsFile ? uri.LocalPath : uri.AbsoluteUri;
		// Replacing the video stops the last one, which can take a moment (a stream closing): not on the UI thread.
		return Task.Run(() => Command(mpv, "loadfile", url, "replace", "-1", fileOptions));
	}

	public override Task SeekAsync(TimeSpan position)
	{
		var mpv = _mpv;
		if (mpv == 0 || !_loaded)
		{
			OnTime(position, report: false);
			return Task.CompletedTask;
		}

		var seconds = Math.Clamp(position.TotalSeconds, 0, Math.Max(0, Duration - 1));
		_atEnd = false;
		Command(mpv, "seek", seconds.ToString(CultureInfo.InvariantCulture), "absolute");
		OnTime(TimeSpan.FromSeconds(seconds));
		return Task.CompletedTask;
	}

	public override bool IsPaused => _paused;

	public override Task SetPausedAsync(bool paused)
	{
		Set("pause", paused ? "yes" : "no");
		return Task.CompletedTask;
	}

	public override Task SetSpeedAsync(double speed)
	{
		Set("speed", speed.ToString(CultureInfo.InvariantCulture));
		return Task.CompletedTask;
	}

	public override IReadOnlyList<MediaTrack> GetTracks()
	{
		var mpv = _mpv;
		if (mpv == 0)
		{
			return [];
		}

		List<MediaTrack> audio = [];
		List<MediaTrack> subtitles = [];
		var count = int.TryParse(GetPropertyString(mpv, "track-list/count"), out var n) ? n : 0;
		for (var i = 0; i < count; i++)
		{
			var type = GetPropertyString(mpv, $"track-list/{i}/type");
			if (type is not ("audio" or "sub") || !int.TryParse(GetPropertyString(mpv, $"track-list/{i}/id"), out var id))
			{
				continue;
			}

			var lang = GetPropertyString(mpv, $"track-list/{i}/lang");
			var name = GetPropertyString(mpv, $"track-list/{i}/title") ?? lang ?? $"Track {id}";
			var track = new MediaTrack(type == "audio" ? MediaTrackKind.Audio : MediaTrackKind.Subtitle, id, name, GetPropertyString(mpv, $"track-list/{i}/selected") == "yes");
			(type == "audio" ? audio : subtitles).Add(track);
		}

		return [.. audio, new MediaTrack(MediaTrackKind.Subtitle, -1, "Off", !subtitles.Any(x => x.IsSelected)), .. subtitles];
	}

	public override Task SelectTrackAsync(MediaTrackKind kind, int id)
	{
		Set(kind == MediaTrackKind.Audio ? "aid" : "sid", id < 0 ? "no" : id.ToString(CultureInfo.InvariantCulture));
		return Task.CompletedTask;
	}

	public override IReadOnlyList<MediaChapter> GetFileChapters()
	{
		var mpv = _mpv;
		if (mpv == 0)
		{
			return [];
		}

		var count = int.TryParse(GetPropertyString(mpv, "chapter-list/count"), out var n) ? n : 0;
		return
		[
			.. Enumerable.Range(0, count).Select(i => new MediaChapter(
				GetPropertyString(mpv, $"chapter-list/{i}/title") ?? "",
				TimeSpan.FromSeconds(double.TryParse(GetPropertyString(mpv, $"chapter-list/{i}/time"), CultureInfo.InvariantCulture, out var t) ? t : 0)))
		];
	}

	public override IReadOnlyList<string> ShaderModes => _modes.Count == 0 ? [] : ["Off", .. _modes.Select(x => x.Name)];

	public override int GetShaderMode()
	{
		var mpv = _mpv;
		if (mpv == 0 || _modes.Count == 0)
		{
			return -1;
		}

		var current = ShaderFiles(GetPropertyString(mpv, "glsl-shaders") ?? "");
		if (current.Length == 0)
		{
			return 0;
		}

		var index = _modes.ToList().FindIndex(x => ShaderFiles(x.Shaders).SequenceEqual(current, StringComparer.OrdinalIgnoreCase));
		return index < 0 ? -1 : index + 1;
	}

	public override void SelectShaderMode(int index)
	{
		var mpv = _mpv;
		if (mpv == 0 || index < 0 || index > _modes.Count)
		{
			return;
		}

		if (index == 0)
		{
			Command(mpv, "change-list", "glsl-shaders", "clr", "");
			_ = ShowMessageAsync("Anime4K: Off", TimeSpan.FromSeconds(2));
			return;
		}

		var mode = _modes[index - 1];
		Command(mpv, "change-list", "glsl-shaders", "set", mode.Shaders);
		_ = ShowMessageAsync(mode.Label, TimeSpan.FromSeconds(2));
	}

	/// <summary>The shader modes bound in an input.conf: each "change-list glsl-shaders set" with the text it shows.</summary>
	internal static IReadOnlyList<ShaderMode> ReadShaderModes(string inputConf)
	{
		try
		{
			return File.Exists(inputConf) ? ParseShaderModes(File.ReadAllText(inputConf)) : [];
		}
		catch (IOException)
		{
			return [];
		}
	}

	internal static IReadOnlyList<ShaderMode> ParseShaderModes(string inputConf)
	{
		return
		[
			.. ShaderModeLine().Matches(inputConf)
				.Where(x => !x.Value.TrimStart().StartsWith('#'))
				.Select(x => new ShaderMode(x.Groups["label"].Value, x.Groups["shaders"].Value))
				.DistinctBy(x => x.Shaders, StringComparer.OrdinalIgnoreCase)
		];
	}

	// The file names, in order: mpv may give the list back joined differently, with its paths expanded.
	private static string[] ShaderFiles(string shaders) =>
		[.. shaders.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Path.GetFileName).OfType<string>()];

	[System.Text.RegularExpressions.GeneratedRegex(@"^[^\r\n]*change-list\s+glsl-shaders\s+set\s+""(?<shaders>[^""]+)""\s*;\s*show-text\s+""(?<label>[^""]+)""", System.Text.RegularExpressions.RegexOptions.Multiline)]
	private static partial System.Text.RegularExpressions.Regex ShaderModeLine();

	// mpv's sub-pos: where the subtitles' bottom goes, in % of the window's height (100 is the bottom). ASS ones move too.
	public override void SetSubtitleLift(double fraction)
	{
		_subPos = Math.Round(100 - Math.Clamp(fraction, 0, 1) * 100).ToString(CultureInfo.InvariantCulture);
		Set("sub-pos", _subPos);
	}

	protected override void TogglePlayPause()
	{
		var mpv = _mpv;
		if (mpv == 0)
		{
			return;
		}

		if (_atEnd)
		{
			_atEnd = false;
			Command(mpv, "seek", "0", "absolute");
			Set("pause", "no");
			return;
		}

		Command(mpv, "cycle", "pause");
	}

	public override void ApplyAspectRatio()
	{
		Set("keepaspect", AspectRatio == PlayerAspectRatio.Stretch ? "no" : "yes");
		Set("video-aspect-override", AspectRatio switch
		{
			PlayerAspectRatio.Ratio16_9 => "16:9",
			PlayerAspectRatio.Ratio4_3 => "4:3",
			PlayerAspectRatio.Ratio21_9 => "21:9",
			_ => "no",
		});
	}

	protected override void ApplyVolume()
	{
		Set("volume", ((int)Volume).ToString(CultureInfo.InvariantCulture));
		Set("mute", IsMuted ? "yes" : "no");
	}

	protected override async Task CloseEngineAsync()
	{
		var mpv = _mpv;
		if (mpv == 0)
		{
			return;
		}

		_mpv = 0;
		// Quitting ends the event loop (a shutdown event); mpv is freed once it's out of mpv_wait_event.
		Command(mpv, "quit");
		if (_events is { } events)
		{
			await events;
		}

		await Task.Run(() => TerminateDestroy(mpv));
	}

	private void Set(string name, string value)
	{
		if (_mpv is var mpv && mpv != 0)
		{
			SetPropertyString(mpv, name, value);
		}
	}

	// On its own thread until mpv shuts down: events are brought to the UI thread, as libvlc's are.
	private unsafe void FollowEvents(nint mpv)
	{
		while (true)
		{
			var e = WaitEvent(mpv);
			switch (e->Id)
			{
				case EventShutdown:
					return;
				case EventStartFile:
					_loaded = false;
					break;
				case EventFileLoaded:
					_loaded = true;
					// With their names and languages; the first is shown. Fetched with the video's headers (its per-file options).
					foreach (var (subtitle, i) in Subtitles.Select((x, i) => (x, i)))
					{
						var url = subtitle.Url.IsFile ? subtitle.Url.LocalPath : subtitle.Url.AbsoluteUri;
						Command(mpv, "sub-add", url, i == 0 ? "select" : "auto", subtitle.Name, subtitle.Language ?? "");
					}

					break;
				case EventVideoReconfig:
					OnUi(() => MoveBehindXaml(_window));
					break;
				case EventPlaybackRestart:
					OnUi(() =>
					{
						if (_paused)
						{
							IsBuffering = false;
						}
						else
						{
							OnPlaying();
						}
					});
					break;
				case EventEndFile when ((EndFileData*)e->Data)->Reason == EndFileReasonError:
					OnUi(() =>
					{
						ShowStatus("Couldn't play the video.", isError: true);
						OnEnded();
					});
					break;
				case EventPropertyChange:
					OnPropertyChange(mpv, (PropertyData*)e->Data);
					break;
			}
		}
	}

	// mpv is the loop's own handle: _mpv is zeroed as closing starts, before the loop has ended.
	private unsafe void OnPropertyChange(nint mpv, PropertyData* property)
	{
		if (property->Format == FormatNone)
		{
			return;
		}

		var name = Marshal.PtrToStringUTF8((nint)property->Name);
		if (property->Format == FormatDouble)
		{
			var value = *(double*)property->Data;
			// The XAML starts 1px below the window's top (WinUI keeps it, full screen too), so the title bar's shade doesn't
			// reach the top row: the video stays off it (black, as under the other players), not a bright line over the shade.
			if (name == "osd-height" && value > 0)
			{
				SetPropertyString(mpv, "video-margin-ratio-top", (1 / value).ToString(CultureInfo.InvariantCulture));
			}
			else if (name == "duration")
			{
				OnUi(() => OnLength(TimeSpan.FromSeconds(value)));
			}
			// Every frame: reported a few times a second, as libvlc does, or straight away after a jump back.
			else if (name == "time-pos" && (value < _reported || value - _reported >= 0.25))
			{
				_reported = value;
				OnUi(() => OnTime(TimeSpan.FromSeconds(value)));
			}

			return;
		}

		var flag = *(int*)property->Data != 0;
		switch (name)
		{
			case "pause":
				_paused = flag;
				OnUi(() => IsPlaying = !flag && _loaded && !_atEnd);
				break;
			case "paused-for-cache":
				OnUi(() => IsBuffering = flag);
				break;
			case "eof-reached" when flag && _loaded:
				_atEnd = true;
				OnUi(OnEnded);
				break;
		}
	}

	/// <summary>
	/// Puts mpv's window (made inside the player's as the video starts) under the XAML's, so the controls draw over the video.
	/// </summary>
	private static unsafe void MoveBehindXaml(nint window)
	{
		var buffer = stackalloc char[16];
		for (var child = GetWindow(window, GwChild); child != 0; child = GetWindow(child, GwHwndNext))
		{
			var length = GetClassName(child, buffer, 16);
			if (new ReadOnlySpan<char>(buffer, Math.Max(0, length)).SequenceEqual("mpv"))
			{
				SetWindowPos(child, 1 /* HWND_BOTTOM */, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010 /* NOSIZE | NOMOVE | NOACTIVATE */);
			}
		}
	}

	private const uint GwHwndNext = 2;
	private const uint GwChild = 5;

	[LibraryImport("user32.dll")]
	private static partial nint GetWindow(nint window, uint command);

	[LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
	private static unsafe partial int GetClassName(nint window, char* name, int length);

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
