namespace Totoro.Core.Plugins;

public interface IPluginLoader
{
	/// <summary>
	/// Discovers and loads plugins from the standard plugins directory and any additional paths.
	/// </summary>
	IReadOnlyList<PluginInfo> LoadPlugins(IEnumerable<string>? additionalSearchPaths = null);

	/// <summary>
	/// All plugins discovered during loading, both successfully loaded and those with errors.
	/// </summary>
	IReadOnlyList<PluginInfo> Plugins { get; }
}
