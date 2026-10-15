namespace Totoro.Core.Plugins;

public interface IPluginManager
{
	/// <summary>
	/// All discovered and loaded plugins.
	/// </summary>
	IReadOnlyList<PluginInfo> InstalledPlugins { get; }

	/// <summary>
	/// Whether a plugin was installed or uninstalled requiring an application restart.
	/// </summary>
	bool RequiresRestart { get; }

	Task<IReadOnlyList<PluginCatalogItem>> GetCatalogAsync(CancellationToken ct = default);
	Task<PluginInfo> InstallFromFileAsync(string packageFilePath, CancellationToken ct = default);
	Task<PluginInfo> InstallFromUrlAsync(Uri packageUrl, IProgress<double>? progress = null, CancellationToken ct = default);
	Task UninstallAsync(Guid pluginId, CancellationToken ct = default);
}
