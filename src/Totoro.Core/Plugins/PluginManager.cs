using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Totoro.Core.Helpers;
using Totoro.Core.Settings;

namespace Totoro.Core.Plugins;

public sealed class PluginManager : IPluginManager
{
	private const string DevPathsKey = "DevPluginPaths";
	private const string CatalogUrlKey = "PluginCatalogUrl";
	private const string DefaultCatalogUrl = "https://raw.githubusercontent.com/insomniachi/Totoro.Plugins/main/plugins.json";

	private readonly IPluginLoader _pluginLoader;
	private readonly IPreferences _preferences;
	private readonly HttpClient _httpClient;
	private readonly ILogger<PluginManager> _logger;

	private readonly List<string> _devPluginPaths;
	private bool _requiresRestart;

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

		_devPluginPaths = _preferences.ReadKey<List<string>>(DevPathsKey) ?? [];
	}

	public IReadOnlyList<PluginInfo> InstalledPlugins => _pluginLoader.Plugins;

	public IReadOnlyList<string> DevPluginPaths => _devPluginPaths;

	public bool RequiresRestart => _requiresRestart;

	public void AddDevPluginPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) return;
		var full = Path.GetFullPath(path);
		if (!_devPluginPaths.Contains(full, StringComparer.OrdinalIgnoreCase))
		{
			_devPluginPaths.Add(full);
			_preferences.SaveKey(DevPathsKey, _devPluginPaths);
			_requiresRestart = true;
		}
	}

	public void RemoveDevPluginPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) return;
		var full = Path.GetFullPath(path);
		if (_devPluginPaths.RemoveAll(p => p.Equals(full, StringComparison.OrdinalIgnoreCase)) > 0)
		{
			_preferences.SaveKey(DevPathsKey, _devPluginPaths);
			_requiresRestart = true;
		}
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

		// Annotate with local installation state
		var installedMap = InstalledPlugins.ToDictionary(p => p.Id, p => p);
		foreach (var item in items)
		{
			if (installedMap.TryGetValue(item.Id, out var installed))
			{
				item.IsInstalled = true;
				item.InstalledVersion = installed.Version;
				item.IsDev = installed.IsDev;
				if (Version.TryParse(item.Version, out var catalogVer) &&
					Version.TryParse(installed.Version, out var installedVer))
				{
					item.HasUpdate = catalogVer > installedVer;
				}
			}
		}

		return items;
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

			var manifestJson = await File.ReadAllTextAsync(manifestPath, ct);
			var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson)
				?? throw new InvalidOperationException("Failed to parse plugin.json manifest from package.");

			var pluginsRoot = FileHelper.GetPath("Plugins");
			var targetDir = Path.Combine(pluginsRoot, manifest.Name);
			Directory.CreateDirectory(pluginsRoot);

			if (Directory.Exists(targetDir))
			{
				try
				{
					Directory.Delete(targetDir, recursive: true);
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Could not completely delete existing folder {TargetDir}, overwriting files.", targetDir);
				}
			}

			Directory.CreateDirectory(targetDir);

			// Copy all extracted files to target directory
			CopyDirectory(tempExtractDir, targetDir);

			_requiresRestart = true;

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
				// Ignore cleanup failures in temp
			}
		}
	}

	public async Task<PluginInfo> InstallFromUrlAsync(Uri packageUrl, IProgress<double>? progress = null, CancellationToken ct = default)
	{
		var tempPackageFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".totoro-plugin");

		try
		{
			using var response = await _httpClient.GetAsync(packageUrl, HttpCompletionOption.ResponseHeadersRead, ct);
			response.EnsureSuccessStatusCode();

			var totalBytes = response.Content.Headers.ContentLength;
			await using var stream = await response.Content.ReadAsStreamAsync(ct);
			await using var fileStream = new FileStream(tempPackageFile, FileMode.Create, FileAccess.Write, FileShare.None);

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

			fileStream.Close();
			return await InstallFromFileAsync(tempPackageFile, ct);
		}
		finally
		{
			if (File.Exists(tempPackageFile))
			{
				try { File.Delete(tempPackageFile); } catch { }
			}
		}
	}

	public Task UninstallAsync(Guid pluginId, CancellationToken ct = default)
	{
		var plugin = InstalledPlugins.FirstOrDefault(p => p.Id == pluginId);
		if (plugin is null)
		{
			return Task.CompletedTask;
		}

		var standardPluginsDir = FileHelper.GetPath("Plugins");
		if (plugin.DirectoryPath.StartsWith(standardPluginsDir, StringComparison.OrdinalIgnoreCase))
		{
			try
			{
				Directory.Delete(plugin.DirectoryPath, recursive: true);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Could not delete plugin folder {Dir} immediately (file may be in use). Marking for pending deletion on restart.", plugin.DirectoryPath);
				var marker = Path.Combine(plugin.DirectoryPath, ".delete");
				File.WriteAllText(marker, "pending-delete");
			}
		}
		else if (plugin.IsDev)
		{
			RemoveDevPluginPath(plugin.DirectoryPath);
		}

		_requiresRestart = true;
		return Task.CompletedTask;
	}

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
