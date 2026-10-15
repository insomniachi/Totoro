using System.Reflection;
using System.Runtime.Loader;

namespace Totoro.Core.Plugins;

/// <summary>
/// An isolated AssemblyLoadContext for a plugin, resolving its private dependencies
/// from the plugin directory while delegating core host contracts (such as Totoro.Core)
/// to the default AssemblyLoadContext.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
	private readonly AssemblyDependencyResolver _resolver;
	private readonly string _pluginPath;
	private readonly string _pluginDirectory;

	public PluginLoadContext(string pluginMainAssemblyPath)
		: base(name: Path.GetFileNameWithoutExtension(pluginMainAssemblyPath), isCollectible: false)
	{
		_pluginPath = pluginMainAssemblyPath;
		_pluginDirectory = Path.GetDirectoryName(pluginMainAssemblyPath) ?? "";
		_resolver = new AssemblyDependencyResolver(pluginMainAssemblyPath);
	}

	public string PluginPath => _pluginPath;
	public string PluginDirectory => _pluginDirectory;

	protected override Assembly? Load(AssemblyName assemblyName)
	{
		// Shared framework and core contracts must be loaded from the default host context
		// so that interfaces (IAnimeProvider, IModule, IServiceCollection) share identical type identity.
		if (IsHostSharedAssembly(assemblyName.Name))
		{
			return null;
		}

		// First try using the .deps.json resolver
		string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
		if (assemblyPath != null)
		{
			return LoadFromAssemblyPath(assemblyPath);
		}

		// Fallback: check if the DLL is located directly in the plugin directory
		if (!string.IsNullOrEmpty(_pluginDirectory) && !string.IsNullOrEmpty(assemblyName.Name))
		{
			string candidate = Path.Combine(_pluginDirectory, assemblyName.Name + ".dll");
			if (File.Exists(candidate))
			{
				return LoadFromAssemblyPath(candidate);
			}
		}

		return null;
	}

	protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
	{
		string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
		if (libraryPath != null)
		{
			return LoadUnmanagedDllFromPath(libraryPath);
		}

		if (!string.IsNullOrEmpty(_pluginDirectory))
		{
			string candidate = Path.Combine(_pluginDirectory, unmanagedDllName);
			if (File.Exists(candidate))
			{
				return LoadUnmanagedDllFromPath(candidate);
			}
		}

		return IntPtr.Zero;
	}

	private static bool IsHostSharedAssembly(string? name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return false;
		}

		return name.Equals("Totoro.Core", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("HtmlAgilityPack", StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase)
			|| name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("System", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("netstandard", StringComparison.OrdinalIgnoreCase)
			|| name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase);
	}
}
