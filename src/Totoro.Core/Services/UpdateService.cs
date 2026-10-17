using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
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

	private static readonly HttpClient s_http = CreateClient();

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
		using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
		using var response = await s_http.SendAsync(request, ct);
		if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			return null;
		}

		response.EnsureSuccessStatusCode();
		using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
		var release = json.RootElement;
		var tag = release.GetProperty("tag_name").GetString() ?? "";
		if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version))
		{
			return null;
		}

		foreach (var asset in release.GetProperty("assets").EnumerateArray())
		{
			var name = asset.GetProperty("name").GetString() ?? "";
			if (!name.EndsWith(InstallerSuffix, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			var digest = asset.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
			return new AppRelease(
				version,
				release.GetProperty("name").GetString() is { Length: > 0 } title ? title : tag,
				new Uri(release.GetProperty("html_url").GetString()!),
				new Uri(asset.GetProperty("browser_download_url").GetString()!),
				digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true ? digest["sha256:".Length..] : null,
				asset.GetProperty("size").GetInt64(),
				release.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "");
		}

		return null;
	}

	/// <summary>Downloads the installer into the temp folder, checking it against GitHub's hash; returns its path.</summary>
	private async Task<string> DownloadAsync(AppRelease release, CancellationToken ct)
	{
		var folder = Path.Combine(Path.GetTempPath(), "Totoro", "Updates");
		Directory.CreateDirectory(folder);
		var path = Path.Combine(folder, Path.GetFileName(release.Installer.LocalPath));

		using (var response = await s_http.GetAsync(release.Installer, HttpCompletionOption.ResponseHeadersRead, ct))
		{
			response.EnsureSuccessStatusCode();
			var length = response.Content.Headers.ContentLength ?? release.Size;
			await using var source = await response.Content.ReadAsStreamAsync(ct);
			await using var file = File.Create(path);
			var buffer = new byte[81920];
			long read = 0;
			int count;
			while ((count = await source.ReadAsync(buffer, ct)) > 0)
			{
				await file.WriteAsync(buffer.AsMemory(0, count), ct);
				read += count;
				// In steps, not every few kilobytes: it's bound to the UI.
				var progress = length > 0 ? (double)read / length : 0;
				if (progress - Progress >= 0.005 || progress >= 1)
				{
					Progress = progress;
				}
			}
		}

		if (release.Sha256 is { } expected)
		{
			await using var file = File.OpenRead(path);
			var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
			if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
			{
				File.Delete(path);
				throw new InvalidDataException("The download doesn't match the release's installer.");
			}
		}

		return path;
	}

	private static Version GetCurrentVersion() => AppInfo.Version;

	private static HttpClient CreateClient()
	{
		var client = new HttpClient();
		// GitHub's API refuses requests without one.
		client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Totoro", GetCurrentVersion().ToString()));
		return client;
	}
}
