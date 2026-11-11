using System.Diagnostics;
using System.Globalization;
using Totoro.Core.Settings;
using Totoro.Helpers;

namespace Totoro.Playback;

/// <summary>
/// Downloads an HLS stream (its best video and audio) with yt-dlp, into an MPEG-TS file, or one ffmpeg merged when its
/// audio is apart from its video. It connects as Chrome does: Cloudflare turns away other clients (AnimePahe's CDN answers
/// .NET, ffmpeg and curl with 403), but not browsers.
/// </summary>
internal sealed class YtDlpDownloader(IPreferences preferences)
{
	/// <summary>Downloads <paramref name="url"/> to <paramref name="output"/>; returns the file's extension.</summary>
	public async Task<string> DownloadAsync(Uri url, IReadOnlyDictionary<string, string> headers, string output,
											IProgress<DownloadProgress> progress, CancellationToken ct)
	{
		var ytDlp = FindYtDlp() ?? throw new DownloadException(
			"Downloading this stream (HLS) needs yt-dlp. Install it (e.g. \"winget install yt-dlp\") or choose yt-dlp.exe in Settings, then download again.");

		var startInfo = new ProcessStartInfo(ytDlp)
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		// Its own HLS downloader (8 fragments at a time) rather than ffmpeg, which would be turned away; nothing to fix up
		// afterwards, so it needs no ffmpeg at all. A few fragments at a time, backing off when the CDN says too many
		// (429), and failing rather than skipping one it can't get, which would leave a gap in the episode. % starts a
		// field in yt-dlp's output template.
		string[] args =
		[
			"--no-config", "--no-playlist", "--impersonate", "chrome", "--hls-prefer-native", "--fixup", "never", "--newline",
			"-N", "3", "--fragment-retries", "15", "--retry-sleep", "fragment:exp=2:60", "--abort-on-unavailable-fragments",
			"--progress-template", "download:[totoro] %(progress.fragment_index)s/%(progress.fragment_count)s %(progress.downloaded_bytes)s %(progress.total_bytes_estimate)s",
			"-o", output.Replace("%", "%%", StringComparison.Ordinal),
		];
		foreach (var arg in args)
		{
			startInfo.ArgumentList.Add(arg);
		}

		// To merge a stream whose audio is apart from its video (KickAssAnime's): mpv's download comes with ffmpeg, else
		// yt-dlp looks for one on the PATH.
		if (File.Exists(Path.Combine(Modules.MediaPlayers.Mpv.MpvInstaller.Folder, "ffmpeg.exe")))
		{
			startInfo.ArgumentList.Add("--ffmpeg-location");
			startInfo.ArgumentList.Add(Modules.MediaPlayers.Mpv.MpvInstaller.Folder);
		}

		foreach (var (name, value) in headers)
		{
			startInfo.ArgumentList.Add("--add-header");
			startInfo.ArgumentList.Add($"{name}:{value}");
		}

		startInfo.ArgumentList.Add(url.AbsoluteUri);

		using var process = new Process { StartInfo = startInfo };
		var errors = new List<string>();
		// 0 is long ago, so the first line is reported.
		long lastReport = 0;
		var speedMeter = new SpeedMeter();
		process.ErrorDataReceived += (_, e) =>
		{
			if (e.Data is { } line && line.StartsWith("ERROR:", StringComparison.Ordinal))
			{
				lock (errors)
				{
					errors.Add(line["ERROR:".Length..].Trim());
				}
			}
		};
		process.OutputDataReceived += (_, e) =>
		{
			// "[totoro] 12/145 5000000 60000000.0": fragments downloaded, of how many, bytes downloaded, and the size it
			// expects from the fragments so far; "NA" for what isn't known yet. Not its speed: that's the fragment it last
			// reported on's, one of several at once. It prints many a second; like a download's, one a second is reported.
			if (Stopwatch.GetElapsedTime(lastReport) >= DownloadService.ReportInterval &&
				e.Data is { } line && line.StartsWith("[totoro] ", StringComparison.Ordinal) &&
				line["[totoro] ".Length..].Split(' ', '/') is [var done, var count, var downloaded, var total] &&
				int.TryParse(done, out var fragment) && int.TryParse(count, out var fragments) && fragments > 0)
			{
				lastReport = Stopwatch.GetTimestamp();
				var bytes = Number(downloaded);
				progress.Report(new DownloadProgress(Math.Clamp((double)fragment / fragments, 0, 1), speedMeter.Next(bytes), bytes, Number(total)));
			}

			static double? Number(string text) => double.TryParse(text, CultureInfo.InvariantCulture, out var number) ? number : null;
		};

		process.Start();
		ChildProcesses.Add(process);
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		try
		{
			await process.WaitForExitAsync(ct);
		}
		catch (OperationCanceledException)
		{
			process.Kill(entireProcessTree: true);
			await process.WaitForExitAsync(CancellationToken.None);
			throw;
		}

		if (process.ExitCode != 0)
		{
			string reason;
			lock (errors)
			{
				reason = errors.LastOrDefault() ?? $"exit code {process.ExitCode}";
			}

			throw new DownloadException($"yt-dlp couldn't download it: {reason}");
		}

		if (File.Exists(output))
		{
			return ".ts";
		}

		// Merged from separate video and audio, it's the output with the merged format's extension added (episode.part.mp4).
		foreach (var extension in new[] { ".mp4", ".mkv", ".webm" })
		{
			if (File.Exists(output + extension))
			{
				File.Move(output + extension, output);
				return extension;
			}
		}

		// Without ffmpeg, yt-dlp leaves the video and audio apart (episode.part.f3102.mp4...) and succeeds.
		throw new DownloadException("This stream's audio is apart from its video, which takes ffmpeg to put together: download mpv in its settings (it comes with ffmpeg), or install ffmpeg, then download again.");
	}

	/// <summary>The yt-dlp.exe the user chose; if they haven't, the one on the PATH, or the one downloaded with mpv.</summary>
	private string? FindYtDlp()
	{
		if (preferences.YtDlpPath is { Length: > 0 } chosen)
		{
			return File.Exists(chosen) ? chosen : throw new DownloadException($"yt-dlp isn't at {chosen} any more: choose it again on the Downloads page.");
		}

		// The user's and machine's PATH as they are now (from the registry), so yt-dlp installed while Totoro runs is found.
		return new[] { EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine }
			   .SelectMany(x => (Environment.GetEnvironmentVariable("PATH", x) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			   .Select(x => Path.Combine(Environment.ExpandEnvironmentVariables(x), "yt-dlp.exe"))
			   .Append(Path.Combine(Modules.MediaPlayers.Mpv.MpvInstaller.Folder, "yt-dlp.exe"))
			   .FirstOrDefault(File.Exists);
	}
}
