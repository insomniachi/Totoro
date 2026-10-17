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
		// So must anything else the host has, at the plugin's version or later: Totoro.Core's own dependencies (Flurl,
		// CommunityToolkit.Mvvm's messenger, which the app's messages go through), WinRT... A plugin needing a later
		// version than the host's gets its own copy.
		if (IsHostSharedAssembly(assemblyName.Name) || IsProvidedByHost(assemblyName))
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

	/// <summary>The host's assemblies (the runtime's list of them, by name), and their versions once asked for.</summary>
	private static readonly Lazy<Dictionary<string, string>> s_hostAssemblies = new(() =>
		((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "")
			.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
			.DistinctBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase));

	private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Version?> s_hostVersions = new(StringComparer.OrdinalIgnoreCase);

	private static bool IsProvidedByHost(AssemblyName assemblyName)
	{
		if (assemblyName.Name is not { } name || !s_hostAssemblies.Value.TryGetValue(name, out var path))
		{
			return false;
		}

		var hostVersion = s_hostVersions.GetOrAdd(name, _ =>
		{
			try
			{
				return AssemblyName.GetAssemblyName(path).Version;
			}
			catch (Exception)
			{
				return null;
			}
		});

		return hostVersion is not null && (assemblyName.Version is null || hostVersion >= assemblyName.Version);
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
