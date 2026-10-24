using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Totoro.Core.Helpers;
using Totoro.Core.Settings;

namespace Totoro.Core.Plugins;

public sealed class PluginManager : IPluginManager
{
	private const string CatalogUrlKey = "PluginCatalogUrl";
	private const string DefaultCatalogUrl = "https://raw.githubusercontent.com/insomniachi/Totoro.Plugins/main/plugins.json";

	private readonly IPluginLoader _pluginLoader;
	private readonly IPreferences _preferences;
	private readonly HttpClient _httpClient;
	private readonly ILogger<PluginManager> _logger;

	private bool _requiresRestart;

	// What changed since the app started (taking effect at the next start), by plugin id: installed versions, and uninstalls.
	private readonly Dictionary<Guid, (string Version, string Directory)> _installedThisSession = [];
	private readonly HashSet<Guid> _uninstalledThisSession = [];

	public PluginManager(
		IPluginLoader pluginLoader,
		IPreferences preferences,
		HttpClient? httpClient = null,
		ILogger<PluginManager>? logger = null)
	{
		_pluginLoader = pluginLoader;
		_preferences = preferences;
		_httpClient = httpClient ?? new HttpClient();
		_logger = logger ?? NullLogger<PluginManager>.Instance;
	}

	public IReadOnlyList<PluginInfo> InstalledPlugins => _pluginLoader.Plugins;

	public bool RequiresRestart => _requiresRestart;

	/// <summary>An install or uninstall takes effect at the next start: the Plugins page offers a restart.</summary>
	private void SetRequiresRestart()
	{
		_requiresRestart = true;
		WeakReferenceMessenger.Default.Send(new PluginsChangedMessage(RequiresRestart: true));
	}

	public async Task<IReadOnlyList<PluginCatalogItem>> GetCatalogAsync(CancellationToken ct = default)
	{
		var catalogUrl = _preferences.ReadKey(CatalogUrlKey, DefaultCatalogUrl);
		List<PluginCatalogItem> items = [];

		try
		{
			using var response = await _httpClient.GetAsync(catalogUrl, ct);
			if (response.IsSuccessStatusCode)
			{
				var json = await response.Content.ReadAsStringAsync(ct);
				items = JsonSerializer.Deserialize<List<PluginCatalogItem>>(json) ?? [];
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to fetch remote plugin catalog from {Url}", catalogUrl);
		}

		// Include any locally loaded/installed plugins (e.g. from adjacent repo or sideload) that aren't in the remote catalog.
		// Plugins that failed to load all have an empty id, and a plugin can be in both a dev folder and the Plugins folder.
		foreach (var installed in InstalledPlugins.Where(p => p.Id != Guid.Empty).DistinctBy(p => p.Id))
		{
			if (!items.Any(i => i.Id == installed.Id))
			{
				items.Add(new PluginCatalogItem
				{
					Id = installed.Id,
					Name = installed.Name,
					Description = installed.Description,
					Version = installed.Version,
					DownloadUrl = "",
					ModuleTypes = installed.Manifest.ModuleTypes,
				});
			}
		}

		foreach (var item in items)
		{
			Annotate(item);
		}

		return items;
	}

	public void Annotate(PluginCatalogItem item)
	{
		var loaded = InstalledPlugins.FirstOrDefault(p => p.Id == item.Id);
		string? installedVersion = null;
		if (_installedThisSession.TryGetValue(item.Id, out var session))
		{
			installedVersion = session.Version;
		}
		else if (loaded is not null && !_uninstalledThisSession.Contains(item.Id))
		{
			installedVersion = loaded.Version;
		}

		item.IsInstalled = installedVersion is not null;
		item.InstalledVersion = installedVersion;
		item.IsDev = loaded?.IsDev == true;
		item.LocalIcon = loaded?.Module?.Descriptor.Image;
		item.LoadError = item.IsInstalled && !_installedThisSession.ContainsKey(item.Id) ? loaded?.LoadError : null;
		item.HasUpdate = item.IsInstalled
			&& Version.TryParse(item.Version, out var catalogVer)
			&& Version.TryParse(installedVersion, out var installedVer)
			&& catalogVer > installedVer;
	}

	public async Task<PluginInfo> InstallFromFileAsync(string packageFilePath, CancellationToken ct = default)
	{
		if (!File.Exists(packageFilePath))
		{
			throw new FileNotFoundException("Plugin package file not found.", packageFilePath);
		}

		var tempExtractDir = Path.Combine(Path.GetTempPath(), "Totoro_Plugin_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempExtractDir);

		try
		{
			ZipFile.ExtractToDirectory(packageFilePath, tempExtractDir, overwriteFiles: true);

			var manifestPath = Path.Combine(tempExtractDir, "plugin.json");
			if (!File.Exists(manifestPath))
			{
				throw new InvalidOperationException("Package does not contain a valid plugin.json manifest.");
			}

			var json = await File.ReadAllTextAsync(manifestPath, ct);
			var manifest = JsonSerializer.Deserialize<PluginManifest>(json)
				?? throw new InvalidOperationException("Could not deserialize plugin manifest.");
			if (!AppInfo.IsAtLeast(manifest.MinAppVersion))
			{
				throw new InvalidOperationException($"{manifest.Name} {manifest.Version} needs Totoro {manifest.MinAppVersion} or later (this is {AppInfo.Version}). Update Totoro first.");
			}

			var targetDir = GetInstallDirectory(manifest);

			// A loaded plugin's assemblies stay locked until the app ends (its load context can't be unloaded), so the new
			// version is staged next to it and swapped in at the next start (see PluginLoader.ApplyPendingInstalls).
			var isLoaded = InstalledPlugins.Any(p => p.LoadContext is not null && IsSameDirectory(p.DirectoryPath, targetDir));
			var copyDir = targetDir;
			if (isLoaded)
			{
				copyDir = targetDir + PluginLoader.PendingSuffix;
			}
			else if (Directory.Exists(targetDir))
			{
				try
				{
					Directory.Delete(targetDir, recursive: true);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Could not delete old plugin dir {TargetDir}; staging the new version for the next start.", targetDir);
					copyDir = targetDir + PluginLoader.PendingSuffix;
				}
			}

			if (Directory.Exists(copyDir))
			{
				Directory.Delete(copyDir, recursive: true);
			}

			Directory.CreateDirectory(copyDir);
			CopyDirectory(tempExtractDir, copyDir);
			// An uninstall before this install, without a restart in between, marked the old folder for deletion.
			if (copyDir == targetDir)
			{
				File.Delete(Path.Combine(targetDir, PluginLoader.DeleteMarker));
			}

			_installedThisSession[manifest.Id] = (manifest.Version, targetDir);
			_uninstalledThisSession.Remove(manifest.Id);
			SetRequiresRestart();

			return new PluginInfo
			{
				Manifest = manifest,
				DirectoryPath = targetDir,
				AssemblyPath = Path.Combine(targetDir, manifest.EntryPoint),
				IsDev = false
			};
		}
		finally
		{
			try
			{
				if (Directory.Exists(tempExtractDir))
				{
					Directory.Delete(tempExtractDir, recursive: true);
				}
			}
			catch
			{
				// Ignore temp folder cleanup failure
			}
		}
	}

	public Task<PluginInfo> InstallAsync(PluginCatalogItem item, IProgress<double>? progress = null, CancellationToken ct = default)
	{
		if (!item.IsCompatible)
		{
			throw new InvalidOperationException($"{item.Name} {item.Version} needs Totoro {item.MinAppVersion} or later (this is {AppInfo.Version}). Update Totoro first.");
		}

		if (!Uri.TryCreate(item.DownloadUrl, UriKind.Absolute, out var url))
		{
			throw new InvalidOperationException($"{item.Name} has no package to download.");
		}

		return InstallFromUrlAsync(url, progress, ct, item.Sha256);
	}

	public async Task<PluginInfo> InstallFromUrlAsync(Uri packageUrl, IProgress<double>? progress = null, CancellationToken ct = default, string? sha256 = null)
	{
		var tempPackagePath = Path.Combine(Path.GetTempPath(), $"totoro_plugin_{Guid.NewGuid():N}.totoro-plugin");
		try
		{
			using var response = await _httpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead, ct);
			response.EnsureSuccessStatusCode();

			var totalBytes = response.Content.Headers.ContentLength;
			await using var stream = await response.Content.ReadAsStreamAsync(ct);
			await using var fileStream = File.Create(tempPackagePath);

			var buffer = new byte[81920];
			long totalRead = 0;
			int bytesRead;

			while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
			{
				await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
				totalRead += bytesRead;
				if (totalBytes.HasValue && totalBytes.Value > 0)
				{
					progress?.Report((double)totalRead / totalBytes.Value);
				}
			}

			await fileStream.FlushAsync(ct);
			fileStream.Close();

			if (!string.IsNullOrWhiteSpace(sha256))
			{
				await using var package = File.OpenRead(tempPackagePath);
				var actual = Convert.ToHexString(await SHA256.HashDataAsync(package, ct));
				if (!actual.Equals(sha256.Trim(), StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException("The downloaded package isn't the one the catalog lists (its SHA-256 differs), so it wasn't installed.");
				}
			}

			return await InstallFromFileAsync(tempPackagePath, ct);
		}
		finally
		{
			try
			{
				if (File.Exists(tempPackagePath))
				{
					File.Delete(tempPackagePath);
				}
			}
			catch
			{
				// Ignore temp file cleanup failure
			}
		}
	}

	public Task UninstallAsync(Guid pluginId, CancellationToken ct = default)
	{
		var loaded = InstalledPlugins.FirstOrDefault(p => p.Id == pluginId && !p.IsDev)
			?? InstalledPlugins.FirstOrDefault(p => p.Id == pluginId);
		string directory;
		if (_installedThisSession.TryGetValue(pluginId, out var installed))
		{
			directory = installed.Directory;
		}
		else if (loaded is not null && !_uninstalledThisSession.Contains(pluginId))
		{
			if (loaded.IsDev || !IsInPluginsDirectory(loaded.DirectoryPath))
			{
				throw new InvalidOperationException($"{loaded.Name} is a dev plugin, loaded from {loaded.DirectoryPath}: remove it there, or start Totoro without --dev.");
			}

			directory = loaded.DirectoryPath;
		}
		else
		{
			throw new InvalidOperationException("That plugin isn't installed.");
		}

		// An update staged for the next start goes too.
		var pending = directory + PluginLoader.PendingSuffix;
		if (Directory.Exists(pending))
		{
			try
			{
				Directory.Delete(pending, recursive: true);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Could not delete the staged update {Dir}.", pending);
			}
		}

		// A loaded plugin's assemblies are locked until the app ends (deleting would only take some of its files), so it's
		// marked to be deleted at the next start.
		var isLocked = loaded?.LoadContext is not null && IsSameDirectory(loaded.DirectoryPath, directory);
		var deleted = false;
		if (!isLocked && Directory.Exists(directory))
		{
			try
			{
				Directory.Delete(directory, recursive: true);
				deleted = true;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Could not delete plugin folder {Dir}; marking it for deletion at the next start.", directory);
			}
		}

		if (!deleted && Directory.Exists(directory))
		{
			File.WriteAllText(Path.Combine(directory, PluginLoader.DeleteMarker), "pending-delete");
		}

		_installedThisSession.Remove(pluginId);
		_uninstalledThisSession.Add(pluginId);
		SetRequiresRestart();
		return Task.CompletedTask;
	}

	/// <summary>
	/// The plugin's folder in the Plugins folder, named after it. A name that isn't a plain folder name (empty, "..", or
	/// with a path separator) would put the plugin, and the delete of its old version, somewhere else, so the id is used
	/// instead.
	/// </summary>
	private static string GetInstallDirectory(PluginManifest manifest)
	{
		var pluginsDir = Path.GetFullPath(FileHelper.GetPath("Plugins"));
		var name = manifest.Name?.Trim() ?? "";
		var isPlainName = name.Length > 0
			&& name is not ("." or "..")
			&& !name.EndsWith(PluginLoader.PendingSuffix, StringComparison.OrdinalIgnoreCase)
			&& name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
		if (!isPlainName)
		{
			if (manifest.Id == Guid.Empty)
			{
				throw new InvalidOperationException("The plugin's manifest has neither a valid name nor an id.");
			}

			name = manifest.Id.ToString("N");
		}

		var targetDir = Path.GetFullPath(Path.Combine(pluginsDir, name));
		if (!IsSameDirectory(Path.GetDirectoryName(targetDir) ?? "", pluginsDir))
		{
			throw new InvalidOperationException($"The plugin's name '{manifest.Name}' isn't a valid folder name.");
		}

		return targetDir;
	}

	private static bool IsInPluginsDirectory(string directory)
	{
		var pluginsDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(FileHelper.GetPath("Plugins"))) + Path.DirectorySeparatorChar;
		return Path.GetFullPath(directory).StartsWith(pluginsDir, StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsSameDirectory(string a, string b) =>
		string.Equals(
			Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
			Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
			StringComparison.OrdinalIgnoreCase);

	private static void CopyDirectory(string sourceDir, string destinationDir)
	{
		foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
		{
			Directory.CreateDirectory(dir.Replace(sourceDir, destinationDir));
		}

		foreach (var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
		{
			var dest = file.Replace(sourceDir, destinationDir);
			File.Copy(file, dest, overwrite: true);
		}
	}
}
