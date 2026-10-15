namespace Totoro.Core.Plugins;

public interface IPluginManager
{
	/// <summary>
	/// All discovered and loaded plugins.
	/// </summary>
	IReadOnlyList<PluginInfo> InstalledPlugins { get; }

	/// <summary>
	/// Additional local directories scanned for plugins during development.
	/// </summary>
	IReadOnlyList<string> DevPluginPaths { get; }

	/// <summary>
	/// Whether a plugin was installed or uninstalled requiring an application restart.
	/// </summary>
	bool RequiresRestart { get; }

	void AddDevPluginPath(string path);
	void RemoveDevPluginPath(string path);

	Task<IReadOnlyList<PluginCatalogItem>> GetCatalogAsync(CancellationToken ct = default);
	Task<PluginInfo> InstallFromFileAsync(string packageFilePath, CancellationToken ct = default);
	Task<PluginInfo> InstallFromUrlAsync(Uri packageUrl, IProgress<double>? progress = null, CancellationToken ct = default);
	Task UninstallAsync(Guid pluginId, CancellationToken ct = default);
}
