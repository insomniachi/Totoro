using System.ComponentModel;
using Totoro.Core.Agent;
using Totoro.Core.Plugins;

namespace Totoro.Agent;

[Description("A plugin: in the catalog, installed, or both.")]
public sealed record PluginSummary(
	[property: Description("Its id, for install_plugin and uninstall_plugin.")]
	string Id,
	string Name,
	string Description,
	string Author,
	[property: Description("What it adds, e.g. IAnimeProvider (somewhere to watch from).")]
	IReadOnlyList<string> Kinds,
	[property: Description("The catalog's version.")]
	string Version,
	bool IsInstalled,
	[property: Description("The version installed, if it is.")]
	string? InstalledVersion,
	[property: Description("Whether the catalog has a newer version than the one installed.")]
	bool HasUpdate,
	[property: Description("Whether it's a dev plugin, loaded from its own folder: it can't be installed or uninstalled from here.")]
	bool IsDev,
	[property: Description("Whether this version of Totoro can run it; if not, Totoro needs updating first (see check_for_updates).")]
	bool IsCompatible,
	[property: Description("Why it didn't load, if it's installed but didn't.")]
	string? LoadError);

[Description("The plugins, and whether Totoro needs restarting for installs and uninstalls to take effect.")]
public sealed record PluginList(
	IReadOnlyList<PluginSummary> Plugins,
	[property: Description("Whether plugins were installed or uninstalled since Totoro started: they take effect once it restarts (restart_app).")]
	bool RequiresRestart);

/// <summary>Agent tools for the plugins: the catalog, installing and uninstalling them (see <see cref="IPluginManager"/>).</summary>
public sealed class PluginTools(IPluginManager plugins)
{
	[AgentTool("list_plugins")]
	[Description("Lists the plugins in Totoro's catalog and the ones installed (anime providers and so on), with which are installed and which have updates.")]
	public async Task<PluginList> ListPluginsAsync()
	{
		var items = await plugins.GetCatalogAsync();
		return new PluginList([.. items.Select(ToSummary)], plugins.RequiresRestart);
	}

	[AgentTool("install_plugin")]
	[Description("Installs a plugin from the catalog, or updates it to the catalog's version. It takes effect once Totoro restarts (restart_app).")]
	public async Task<string> InstallPluginAsync(
		[Description("The plugin's id or name, from list_plugins.")] string plugin)
	{
		var item = await FindAsync(plugin);
		if (item.IsDev)
		{
			throw new AgentToolException($"{item.Name} is a dev plugin, loaded from its own folder: it isn't installed from the catalog.");
		}

		if (item.IsInstalled && !item.HasUpdate)
		{
			throw new AgentToolException($"{item.Name} {item.InstalledVersion} is installed and up to date.");
		}

		if (string.IsNullOrWhiteSpace(item.DownloadUrl))
		{
			throw new AgentToolException($"{item.Name} isn't in the catalog, so there's nothing to download.");
		}

		var wasInstalled = item.IsInstalled;
		try
		{
			await plugins.InstallAsync(item);
		}
		catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or IOException)
		{
			throw new AgentToolException($"Couldn't install {item.Name}: {ex.Message}");
		}

		return $"{(wasInstalled ? "Updated" : "Installed")} {item.Name} {item.Version}. It takes effect once Totoro restarts (restart_app).";
	}

	[AgentTool("uninstall_plugin", RequiresApproval = true)]
	[Description("Uninstalls a plugin. It's gone once Totoro restarts (restart_app).")]
	public async Task<string> UninstallPluginAsync(
		[Description("The plugin's id or name, from list_plugins.")] string plugin)
	{
		var item = await FindAsync(plugin);
		try
		{
			await plugins.UninstallAsync(item.Id);
		}
		catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
		{
			throw new AgentToolException(ex.Message);
		}

		return $"Uninstalled {item.Name}. It's gone once Totoro restarts (restart_app).";
	}

	[AgentTool("restart_app", RequiresApproval = true)]
	[Description("Restarts Totoro, e.g. so plugins that were installed or uninstalled take effect. This chat ends with it.")]
	public string RestartApp()
	{
		App.Restart();
		return "Restarting Totoro.";
	}

	private async Task<PluginCatalogItem> FindAsync(string plugin)
	{
		var items = await plugins.GetCatalogAsync();
		var key = plugin.Trim();
		return (Guid.TryParse(key, out var id)
				   ? items.FirstOrDefault(x => x.Id == id)
				   : items.FirstOrDefault(x => x.Name.Equals(key, StringComparison.OrdinalIgnoreCase)))
			   ?? throw new AgentToolException($"There's no plugin '{plugin}'. Use list_plugins to see them.");
	}

	private static PluginSummary ToSummary(PluginCatalogItem item) => new(
		item.Id.ToString(),
		item.Name,
		item.Description,
		item.Author,
		item.ModuleTypes,
		item.Version,
		item.IsInstalled,
		item.InstalledVersion,
		item.HasUpdate,
		item.IsDev,
		item.IsCompatible,
		item.LoadError);
}
