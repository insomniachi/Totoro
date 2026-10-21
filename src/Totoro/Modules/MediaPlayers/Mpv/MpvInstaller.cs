using System.Diagnostics;
using System.Security.Cryptography;
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
/// is updating. Adds Anime4K to mpv's config too, as the settings ask. Adds mpv to Windows' Open with menu when the settings ask for it. Used on the UI thread.
/// </summary>
internal sealed partial class MpvInstaller
{
	private const string Repository = "shinchiro/mpv-winbuild-cmake";
	private static readonly Uri s_ytDlp = new("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe");

	// The templates Anime4K's Windows guide links to: its shaders, with an input.conf and mpv.conf that use them. Pinned,
	// with their hashes, as their release has none on GitHub.
	private static readonly Dictionary<Anime4KGpu, (Uri Url, string Hash)> s_anime4K = new()
	{
		[Anime4KGpu.LowerEnd] = (new("https://github.com/Tama47/Anime4K/releases/download/v4.0.1/GLSL_Windows_Low-end.zip"), "a38a4e7cc80bfb76f54445bc52c044de550e8a9bf7edc01494342ba98ddc2dfd"),
		[Anime4KGpu.HigherEnd] = (new("https://github.com/Tama47/Anime4K/releases/download/v4.0.1/GLSL_Windows_High-end.zip"), "f7dc0b1fc6f1f6865ef34ec793d2d7e934bf3fb48c36d5d7a9f88f718d896a6b"),
	};

	// Around the lines added to the user's input.conf and mpv.conf, so they can be swapped or taken out again.
	private const string Anime4KStart = "# Added by Totoro: Anime4K";
	private const string Anime4KEnd = "# End of what Totoro added";

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

	/// <summary>mpv's config folder, where Anime4K's guide installs it, so an mpv of the user's own gets it as well.</summary>
	public static string ConfigFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mpv");

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
			await ApplyAnime4KAsync(folder, ct);
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

	/// <summary>
	/// Adds Anime4K's shaders and the template's lines for the GPU chosen to mpv's config, unless it's there already or
	/// the config has Anime4K lines of the user's own; takes the lines added out again when it's turned off.
	/// </summary>
	private async Task ApplyAnime4KAsync(string folder, CancellationToken ct)
	{
		var gpu = _settings.Value.InstallAnime4K ? _settings.Value.Anime4KGpu : (Anime4KGpu?)null;
		string[] confs = [Path.Combine(ConfigFolder, "input.conf"), Path.Combine(ConfigFolder, "mpv.conf")];
		var texts = confs.Select(x => File.Exists(x) ? File.ReadAllText(x) : "").ToArray();
		var shaders = Path.Combine(ConfigFolder, "shaders");
		var hasShaders = File.Exists(Path.Combine(shaders, "Anime4K_Clamp_Highlights.glsl"));
		if (gpu is null || (hasShaders && texts.All(x => HasAnime4K(x, gpu.Value))))
		{
			for (var i = 0; i < confs.Length; i++)
			{
				if (gpu is null && WithoutAddedAnime4K(texts[i]) is var text && text != texts[i])
				{
					await File.WriteAllTextAsync(confs[i], text, ct);
				}
			}

			return;
		}

		var (url, hash) = s_anime4K[gpu.Value];
		var bytes = await GitHubReleases.Http.GetByteArrayAsync(url, ct);
		if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
		{
			throw new InvalidOperationException("Anime4K's download isn't the one expected.");
		}

		Directory.CreateDirectory(folder);
		var zip = Path.Combine(folder, "anime4k.zip");
		await File.WriteAllBytesAsync(zip, bytes, ct);
		var template = Path.Combine(folder, "anime4k");
		await Task.Run(() => ArchiveFactory.WriteToDirectory(zip, template, new ExtractionOptions { ExtractFullPath = true, Overwrite = true }), ct);

		Directory.CreateDirectory(shaders);
		foreach (var shader in Directory.GetFiles(Path.Combine(template, "shaders"), "*.glsl"))
		{
			File.Copy(shader, Path.Combine(shaders, Path.GetFileName(shader)), true);
		}

		for (var i = 0; i < confs.Length; i++)
		{
			if (!HasAnime4K(texts[i], gpu.Value))
			{
				var lines = await File.ReadAllTextAsync(Path.Combine(template, Path.GetFileName(confs[i])), ct);
				await File.WriteAllTextAsync(confs[i], WithAnime4K(texts[i], gpu.Value, lines), ct);
			}
		}
	}

	/// <summary>Whether a config has the lines added for this GPU, or Anime4K lines of the user's own (left alone).</summary>
	internal static bool HasAnime4K(string conf, Anime4KGpu gpu)
	{
		return conf.Contains($"{Anime4KStart} ({gpu})", StringComparison.Ordinal) || (!conf.Contains(Anime4KStart, StringComparison.Ordinal) && conf.Contains("Anime4K", StringComparison.Ordinal));
	}

	/// <summary>A config with a template's lines in place of any added before; last, so mpv.conf's shaders are the ones played with.</summary>
	internal static string WithAnime4K(string conf, Anime4KGpu gpu, string lines)
	{
		var own = WithoutAddedAnime4K(conf).TrimEnd();
		var nl = Environment.NewLine;
		return $"{own}{(own.Length > 0 ? nl + nl : "")}{Anime4KStart} ({gpu}){nl}{lines.Trim()}{nl}{Anime4KEnd}{nl}";
	}

	internal static string WithoutAddedAnime4K(string conf)
	{
		var start = conf.IndexOf(Anime4KStart, StringComparison.Ordinal);
		var end = start < 0 ? -1 : conf.IndexOf(Anime4KEnd, start, StringComparison.Ordinal);
		if (end < 0)
		{
			return conf;
		}

		var kept = string.Join(Environment.NewLine, ((string[])[conf[..start].Trim(), conf[(end + Anime4KEnd.Length)..].Trim()]).Where(x => x.Length > 0));
		return kept.Length == 0 ? "" : kept + Environment.NewLine;
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
