using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Totoro.Core.Helpers;
using Totoro.Core.Settings;

namespace Totoro.Core.Services;

public enum UpdateState
{
	/// <summary>Not checked yet.</summary>
	Unknown,
	Checking,
	UpToDate,

	/// <summary>A newer version is out: <see cref="UpdateService.Available"/>.</summary>
	Available,
	Downloading,

	/// <summary>Its installer is running; Totoro is closing.</summary>
	Installing,
	Failed,
}

/// <summary>A release of Totoro on GitHub, with its installer.</summary>
/// <param name="Sha256">The installer's SHA-256, as GitHub has it (hex); null if it doesn't.</param>
public sealed record AppRelease(Version Version, string Name, Uri Page, Uri Installer, string? Sha256, long Size, string Notes);

/// <summary>Sent when the app should close, e.g. to let the installer of a newer version replace it.</summary>
public sealed record AppExitRequestedMessage;

/// <summary>
/// Updates Totoro from its GitHub releases: finds the latest (a newer version, with an installer built by the
/// repository's workflow), downloads its installer, checks it against GitHub's hash, and runs it silently. Totoro closes
/// for it, and the installer starts it again. Only an installed copy can be updated (a build from source can still
/// check). Used on the UI thread.
/// </summary>
public sealed partial class UpdateService(IPreferences preferences, ILogger<UpdateService> logger) : ObservableObject
{
	private const string Repository = "insomniachi/Totoro";

	// The installer the workflow builds (installer/Totoro.iss): Totoro-<version>-setup-x64.exe.
	private const string InstallerSuffix = "-setup-x64.exe";

	// After starting, before checking: the app has more to do first.
	private static readonly TimeSpan s_startupDelay = TimeSpan.FromSeconds(10);

	/// <summary>The running version, e.g. 2.0.0.</summary>
	public Version CurrentVersion { get; } = GetCurrentVersion();

	/// <summary>Whether this copy was installed (by the installer), so an update can replace it.</summary>
	public bool CanInstall { get; } = File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

	[ObservableProperty] public partial UpdateState State { get; private set; }

	/// <summary>The newer version, once one's found; null if there isn't one.</summary>
	[ObservableProperty] public partial AppRelease? Available { get; private set; }

	/// <summary>How much of the installer is downloaded, from 0 to 1.</summary>
	[ObservableProperty] public partial double Progress { get; private set; }

	/// <summary>Why checking or updating failed; empty, never null, when it didn't.</summary>
	[ObservableProperty] public partial string Error { get; private set; } = "";

	/// <summary>Checks once the app has started, if the user wants that and this copy can be updated.</summary>
	public async Task CheckOnStartupAsync()
	{
		if (!CanInstall || !preferences.CheckForUpdates)
		{
			return;
		}

		await Task.Delay(s_startupDelay);
		await CheckAsync();
	}

	/// <summary>Looks for a newer version; returns it, or null if this is the latest.</summary>
	[RelayCommand]
	public async Task<AppRelease?> CheckAsync(CancellationToken ct = default)
	{
		if (State is UpdateState.Checking or UpdateState.Downloading or UpdateState.Installing)
		{
			return Available;
		}

		State = UpdateState.Checking;
		Error = "";
		try
		{
			var latest = await GetLatestReleaseAsync(ct);
			Available = latest is not null && latest.Version > CurrentVersion ? latest : null;
			State = Available is null ? UpdateState.UpToDate : UpdateState.Available;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Couldn't check for updates");
			Fail($"Couldn't check for updates: {ex.Message}");
		}

		return Available;
	}

	/// <summary>Downloads the newer version's installer and runs it; Totoro then closes, and the installer reopens it.</summary>
	[RelayCommand]
	public async Task InstallAsync(CancellationToken ct = default)
	{
		if (!CanInstall)
		{
			Fail("Only an installed copy of Totoro can be updated. Download the installer from GitHub instead.");
			return;
		}

		if (State is UpdateState.Downloading or UpdateState.Installing || (Available ?? await CheckAsync(ct)) is not { } release)
		{
			return;
		}

		State = UpdateState.Downloading;
		Progress = 0;
		Error = "";
		try
		{
			var installer = await DownloadAsync(release, ct);
			State = UpdateState.Installing;
			// Silently, with its progress showing: it closes Totoro if it's still running, and starts it again after.
			Process.Start(new ProcessStartInfo(installer, "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS") { UseShellExecute = true });
			WeakReferenceMessenger.Default.Send(new AppExitRequestedMessage());
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogWarning(ex, "Couldn't update to {Version}", release.Version);
			Fail($"Couldn't update: {ex.Message}");
		}
	}

	private void Fail(string error)
	{
		Error = error;
		State = UpdateState.Failed;
	}

	/// <summary>The latest release that has an installer; null if there isn't one.</summary>
	private static async Task<AppRelease?> GetLatestReleaseAsync(CancellationToken ct)
	{
		// The latest that isn't a pre-release. Releases without the installer (the old app's) aren't updates.
		if (await GitHubReleases.GetLatestAsync(Repository, ct) is not { } release ||
			!Version.TryParse(release.Tag.TrimStart('v', 'V'), out var version) ||
			release.Assets.FirstOrDefault(x => x.Name.EndsWith(InstallerSuffix, StringComparison.OrdinalIgnoreCase)) is not { } installer)
		{
			return null;
		}

		return new AppRelease(version, release.Name, release.Page, installer.Url, installer.Sha256, installer.Size, release.Notes);
	}

	/// <summary>Downloads the installer into the temp folder, checking it against GitHub's hash; returns its path.</summary>
	private async Task<string> DownloadAsync(AppRelease release, CancellationToken ct)
	{
		var folder = Path.Combine(Path.GetTempPath(), "Totoro", "Updates");
		Directory.CreateDirectory(folder);
		var name = Path.GetFileName(release.Installer.LocalPath);
		var path = Path.Combine(folder, name);
		await GitHubReleases.DownloadAsync(new GitHubAsset(name, release.Installer, release.Size, release.Sha256), path, new Progress<double>(x => Progress = x), ct);
		return path;
	}

	private static Version GetCurrentVersion() => AppInfo.Version;
}
