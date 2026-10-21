using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Totoro.Core.Helpers;
using Totoro.Core.Settings;

namespace Totoro.Modules.TorrentStreamers.Rqbit;

/// <summary>
/// Downloads rqbit's Windows release from GitHub into the app's data folder, checked against GitHub's hash, and updates
/// it: downloading again is updating. Used on the UI thread.
/// </summary>
internal sealed partial class RqbitInstaller
{
	private const string Repository = "ikatson/rqbit";

	private readonly ILogger<RqbitInstaller> _logger;

	public RqbitInstaller(ILogger<RqbitInstaller> logger)
	{
		_logger = logger;
		Action = new SettingsAction(InstallCommand);
		ShowInstalled();
		DeleteOld();
	}

	/// <summary>Where rqbit goes: the app's data, which updates and reinstalling Totoro leave alone.</summary>
	public static string Folder => FileHelper.GetPath(Path.Combine("Tools", "rqbit"));

	public static string Exe => Path.Combine(Folder, "rqbit.exe");

	public static bool IsInstalled => File.Exists(Exe);

	/// <summary>The release installed (its tag, e.g. v9.0.1); empty if none is.</summary>
	public static string InstalledVersion => IsInstalled && File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : "";

	private static string VersionFile => Path.Combine(Folder, "version.txt");

	/// <summary>The settings' Download/Update button, with how it's going.</summary>
	public SettingsAction Action { get; }

	/// <summary>Why the last download failed; empty, never null, when it didn't.</summary>
	public string Error { get; private set; } = "";

	/// <summary>Downloads the latest rqbit if it's newer than what's here.</summary>
	[RelayCommand]
	public async Task InstallAsync(CancellationToken ct = default)
	{
		if (Action.IsBusy)
		{
			return;
		}

		Action.IsBusy = true;
		Error = "";
		try
		{
			var release = await GitHubReleases.GetLatestAsync(Repository, ct) ?? throw new InvalidOperationException("rqbit has no release on GitHub.");
			if (release.Tag != InstalledVersion)
			{
				var asset = release.Assets.FirstOrDefault(x => x.Name == "rqbit.exe") ?? throw new InvalidOperationException($"rqbit's release {release.Tag} has no rqbit.exe.");
				Directory.CreateDirectory(Folder);
				var download = Exe + ".download";
				await GitHubReleases.DownloadAsync(asset, download, null, ct);
				Replace(download);
				await File.WriteAllTextAsync(VersionFile, release.Tag, ct);
			}

			ShowInstalled();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "Couldn't download rqbit");
			Error = $"Couldn't download rqbit: {ex.Message}";
			Action.Description = Error;
		}
		finally
		{
			Action.IsBusy = false;
		}
	}

	/// <summary>
	/// Puts the download in place of rqbit.exe. One that's running (streaming) can't be replaced, but can be renamed: it
	/// goes on running until the app restarts, and the update is used from then on.
	/// </summary>
	private static void Replace(string download)
	{
		DeleteOld();
		if (IsInstalled)
		{
			File.Move(Exe, OldExe);
		}

		File.Move(download, Exe);
	}

	private static string OldExe => Exe + ".old";

	/// <summary>Deletes the rqbit.exe an update replaced, unless it's still running.</summary>
	private static void DeleteOld()
	{
		try
		{
			File.Delete(OldExe);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
		}
	}

	private void ShowInstalled()
	{
		Action.Label = IsInstalled ? "Update" : "Download";
		Action.Description = InstalledVersion;
	}
}
