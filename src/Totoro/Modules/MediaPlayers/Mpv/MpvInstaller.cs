using System.Diagnostics;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;
using Totoro.Core.Helpers;
using Totoro.Core.Settings;

namespace Totoro.Modules.MediaPlayers.Mpv;

/// <summary>
/// Downloads mpv, with FFmpeg and yt-dlp, into the app's data folder, and updates them: shinchiro's Windows builds from
/// GitHub (what mpv's own updater.bat gets), checked against GitHub's hashes, and yt-dlp's own release. Downloading again
/// is updating. Adds mpv to Windows' Open with menu when the settings ask for it. Used on the UI thread.
/// </summary>
internal sealed partial class MpvInstaller
{
	private const string Repository = "shinchiro/mpv-winbuild-cmake";
	private static readonly Uri s_ytDlp = new("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe");

	private readonly IModuleSettings<Settings> _settings;
	private readonly ILogger<MpvInstaller> _logger;

	// What the Open with menu was last set to, so saving other settings doesn't register mpv again. Changes are only seen
	// once this exists, which the settings make sure of: showing them (the dialog, get_module_settings) reads Action.
	private bool _addedToOpenWith;

	public MpvInstaller(IModuleSettings<Settings> settings, ILogger<MpvInstaller> logger)
	{
		_settings = settings;
		_logger = logger;
		_addedToOpenWith = settings.Value.AddToOpenWith;
		Action = new SettingsAction(InstallCommand);
		ShowInstalled();
		WeakReferenceMessenger.Default.Register<MpvInstaller, ModuleSettingsChangedMessage>(this, (r, m) =>
		{
			if (m.ModuleId == MpvModule.Id)
			{
				_ = r.ApplyOpenWithAsync();
			}
		});
	}

	/// <summary>Where mpv, ffmpeg and yt-dlp go: the app's data, which updates and reinstalling Totoro leave alone.</summary>
	public static string Folder => FileHelper.GetPath(Path.Combine("Tools", "mpv"));

	public static string Exe => Path.Combine(Folder, "mpv.exe");

	public static bool IsInstalled => File.Exists(Exe);

	/// <summary>The release installed (its tag, e.g. 20261020); empty if none is.</summary>
	public static string InstalledVersion => IsInstalled && File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : "";

	private static string VersionFile => Path.Combine(Folder, "version.txt");

	/// <summary>The settings' Download/Update button, with how it's going.</summary>
	public SettingsAction Action { get; }

	/// <summary>Why the last download failed; empty, never null, when it didn't.</summary>
	public string Error { get; private set; } = "";

	/// <summary>Downloads the latest mpv and FFmpeg if they're newer than what's here, and updates yt-dlp.</summary>
	[RelayCommand]
	public async Task InstallAsync(CancellationToken ct = default)
	{
		if (Action.IsBusy)
		{
			return;
		}

		Action.IsBusy = true;
		Error = "";
		var folder = Path.Combine(Path.GetTempPath(), "Totoro", "mpv");
		try
		{
			var release = await GitHubReleases.GetLatestAsync(Repository, ct) ?? throw new InvalidOperationException("mpv's builds have no release on GitHub.");
			if (release.Tag != InstalledVersion)
			{
				var assets = PickAssets(release.Assets) ?? throw new InvalidOperationException($"mpv's release {release.Tag} has no 64-bit mpv and FFmpeg.");
				EnsureNotRunning();

				Directory.CreateDirectory(folder);
				List<string> archives = [];
				foreach (var asset in assets)
				{
					var path = Path.Combine(folder, asset.Name);
					await GitHubReleases.DownloadAsync(asset, path, null, ct);
					archives.Add(path);
				}

				Directory.CreateDirectory(Folder);
				await Task.Run(() =>
				{
					foreach (var archive in archives)
					{
						ArchiveFactory.WriteToDirectory(archive, Folder, new ExtractionOptions { ExtractFullPath = true, Overwrite = true });
					}
				}, ct);
				await File.WriteAllTextAsync(VersionFile, release.Tag, ct);
			}

			await UpdateYtDlpAsync(ct);
			if (_settings.Value.AddToOpenWith)
			{
				// Again after an update too: the entry names this mpv.exe.
				await ApplyOpenWithAsync(always: true);
			}

			ShowInstalled();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "Couldn't download mpv");
			Error = ex is IOException { HResult: unchecked((int)0x80070020) } ? "mpv is open: close it, then try again." : $"Couldn't download mpv: {ex.Message}";
			Action.Description = Error;
		}
		finally
		{
			Action.IsBusy = false;
			try
			{
				Directory.Delete(folder, true);
			}
			catch (IOException)
			{
			}
		}
	}

	/// <summary>The 64-bit mpv and FFmpeg builds among a release's files (not the -v3 builds for newer CPUs, nor mpv-dev); null if one's missing.</summary>
	internal static IReadOnlyList<GitHubAsset>? PickAssets(IEnumerable<GitHubAsset> assets)
	{
		var list = assets.ToList();
		var mpv = list.FirstOrDefault(x => MpvAsset().IsMatch(x.Name));
		var ffmpeg = list.FirstOrDefault(x => FfmpegAsset().IsMatch(x.Name));
		return mpv is null || ffmpeg is null ? null : [mpv, ffmpeg];
	}

	private void ShowInstalled()
	{
		Action.Label = IsInstalled ? "Update" : "Download";
		Action.Description = InstalledVersion;
	}

	/// <summary>mpv.exe can't be replaced while it runs; say so before downloading rather than failing halfway.</summary>
	private static void EnsureNotRunning()
	{
		if (IsInstalled)
		{
			using var _ = File.Open(Exe, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
		}
	}

	private async Task UpdateYtDlpAsync(CancellationToken ct)
	{
		var ytDlp = Path.Combine(Folder, "yt-dlp.exe");
		if (File.Exists(ytDlp))
		{
			await RunAsync(ytDlp, "-U", ct);
			return;
		}

		// Its own release, which has no hash in the URL to check against; yt-dlp -U checks its updates itself.
		var path = ytDlp + ".download";
		await using (var source = await GitHubReleases.Http.GetStreamAsync(s_ytDlp, ct))
		await using (var file = File.Create(path))
		{
			await source.CopyToAsync(file, ct);
		}

		File.Move(path, ytDlp, true);
	}

	/// <summary>Adds mpv to the Open with menu, or takes it off, if that's changed (per user: mpv --register needs no admin).</summary>
	private async Task ApplyOpenWithAsync(bool always = false)
	{
		var add = _settings.Value.AddToOpenWith;
		if (!IsInstalled || (!always && add == _addedToOpenWith))
		{
			return;
		}

		_addedToOpenWith = add;
		try
		{
			await RunAsync(Exe, add ? "--register" : "--unregister", CancellationToken.None);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Couldn't change mpv's Open with entry");
		}
	}

	private static async Task RunAsync(string fileName, string arguments, CancellationToken ct)
	{
		using var process = Process.Start(new ProcessStartInfo(fileName, arguments) { CreateNoWindow = true, UseShellExecute = false })!;
		await process.WaitForExitAsync(ct);
	}

	[GeneratedRegex(@"^mpv-x86_64-\d{8}-git-[0-9a-f]+\.7z$", RegexOptions.IgnoreCase)]
	private static partial Regex MpvAsset();

	[GeneratedRegex(@"^ffmpeg-x86_64-git-[0-9a-f]+\.7z$", RegexOptions.IgnoreCase)]
	private static partial Regex FfmpegAsset();
}
