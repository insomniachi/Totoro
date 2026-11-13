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

	/// <summary>The catalog's plugins and the ones installed otherwise (e.g. dev plugins), each with its state (see <see cref="Annotate"/>).</summary>
	/// <param name="refresh">Download the catalog again instead of using the one fetched earlier this session.</param>
	Task<IReadOnlyList<PluginCatalogItem>> GetCatalogAsync(bool refresh = false, CancellationToken ct = default);

	/// <summary>Why the last catalog download failed, for the user; null if it didn't. <see cref="GetCatalogAsync"/> then has only what's installed (or the last good catalog).</summary>
	string? CatalogError { get; }

	/// <summary>Sets the item's state (installed, which version, an update...), counting what was changed since the app started.</summary>
	void Annotate(PluginCatalogItem item);

	/// <summary>Installs (or updates to) the catalog's version of a plugin, checking the package's hash when it has one.</summary>
	Task<PluginInfo> InstallAsync(PluginCatalogItem item, IProgress<double>? progress = null, CancellationToken ct = default);

	Task<PluginInfo> InstallFromFileAsync(string packageFilePath, CancellationToken ct = default);

	/// <param name="sha256">The package's SHA-256 (hex): the install fails if what's downloaded doesn't have it.</param>
	Task<PluginInfo> InstallFromUrlAsync(Uri packageUrl, IProgress<double>? progress = null, CancellationToken ct = default, string? sha256 = null);

	/// <exception cref="InvalidOperationException">It isn't installed, or is a dev plugin (loaded from its own folder).</exception>
	Task UninstallAsync(Guid pluginId, CancellationToken ct = default);
}

/// <summary>A plugin was installed, updated or uninstalled (taking effect when the app restarts), or the catalog was fetched.</summary>
public sealed record PluginsChangedMessage(bool RequiresRestart);
