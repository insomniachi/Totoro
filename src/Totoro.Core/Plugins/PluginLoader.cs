using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Totoro.Core.Helpers;
using Totoro.Core.Modules;

namespace Totoro.Core.Plugins;

public sealed class PluginLoader(ILogger<PluginLoader>? logger = null) : IPluginLoader
{
	private readonly ILogger<PluginLoader> _logger = logger ?? NullLogger<PluginLoader>.Instance;
	private readonly List<PluginInfo> _plugins = [];

	public IReadOnlyList<PluginInfo> Plugins => _plugins;

	public IReadOnlyList<PluginInfo> LoadPlugins(IEnumerable<string>? additionalSearchPaths = null)
	{
		_plugins.Clear();

		var searchDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// 1. Additional / Dev paths passed explicitly (e.g. from command line args)
		if (additionalSearchPaths is not null)
		{
			foreach (var path in additionalSearchPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
			{
				if (Directory.Exists(path))
				{
					searchDirectories.Add(Path.GetFullPath(path));
				}
			}
		}

		// 2. Environment variables: TOTORO_PLUGIN_DIRS or TOTORO_DEV_PLUGIN_DIR
		var envDirs = Environment.GetEnvironmentVariable("TOTORO_PLUGIN_DIRS")
					  ?? Environment.GetEnvironmentVariable("TOTORO_DEV_PLUGIN_DIR");
		if (!string.IsNullOrWhiteSpace(envDirs))
		{
			foreach (var dir in envDirs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				if (Directory.Exists(dir))
				{
					searchDirectories.Add(Path.GetFullPath(dir));
				}
			}
		}

		// 3. Standard app data plugins folder: %LocalAppData%\Totoro\Plugins
		var standardPluginsDir = FileHelper.GetPath("Plugins");
		if (Directory.Exists(standardPluginsDir))
		{
			searchDirectories.Add(Path.GetFullPath(standardPluginsDir));
		}

		// Discover candidate plugin directories
		var candidatePluginDirs = new List<(string Directory, bool IsDev)>();

		foreach (var dir in searchDirectories)
		{
			bool isDevDir = !dir.Equals(Path.GetFullPath(standardPluginsDir), StringComparison.OrdinalIgnoreCase);

			// Check if this directory itself contains plugin files
			if (IsPluginDirectory(dir))
			{
				candidatePluginDirs.Add((dir, isDevDir));
			}
			else
			{
				// Otherwise, scan subdirectories (including src/ in repository structures)
				try
				{
					foreach (var subDir in Directory.GetDirectories(dir))
					{
						if (IsPluginDirectory(subDir))
						{
							candidatePluginDirs.Add((subDir, isDevDir));
						}
						else if (Path.GetFileName(subDir).Equals("src", StringComparison.OrdinalIgnoreCase) ||
						         Path.GetFileName(subDir).Equals("plugins", StringComparison.OrdinalIgnoreCase))
						{
							foreach (var nested in Directory.GetDirectories(subDir))
							{
								candidatePluginDirs.Add((nested, isDevDir));
							}
						}
						else
						{
							candidatePluginDirs.Add((subDir, isDevDir));
						}
					}
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Failed to inspect directory for plugins: {Directory}", dir);
				}
			}
		}

		var seenPluginIds = new HashSet<Guid>();

		foreach (var (pluginDir, isDev) in candidatePluginDirs)
		{
			var pluginInfo = LoadPluginFromDirectory(pluginDir, isDev);
			if (pluginInfo is null)
			{
				continue;
			}

			if (pluginInfo.IsLoaded && !seenPluginIds.Add(pluginInfo.Id))
			{
				_logger.LogWarning("Duplicate plugin ID {Id} detected in {Directory}; ignoring duplicate.", pluginInfo.Id, pluginDir);
				continue;
			}

			_plugins.Add(pluginInfo);
		}

		return _plugins;
	}

	private static bool IsPluginDirectory(string dir)
	{
		return File.Exists(Path.Combine(dir, "plugin.json"))
			|| Directory.GetFiles(dir, "*.deps.json").Length > 0;
	}

	private PluginInfo? LoadPluginFromDirectory(string directory, bool isDev)
	{
		if (File.Exists(Path.Combine(directory, ".delete")))
		{
			try
			{
				Directory.Delete(directory, recursive: true);
				return null;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to delete plugin marked for removal: {Directory}", directory);
			}
		}

		string manifestPath = Path.Combine(directory, "plugin.json");
		PluginManifest? manifest = null;

		if (File.Exists(manifestPath))
		{
			try
			{
				string json = File.ReadAllText(manifestPath);
				manifest = JsonSerializer.Deserialize<PluginManifest>(json);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to parse plugin.json in {Directory}", directory);
				return new PluginInfo
				{
					Manifest = new PluginManifest
					{
						Id = Guid.Empty,
						Name = Path.GetFileName(directory),
						Version = "0.0.0",
						EntryPoint = ""
					},
					DirectoryPath = directory,
					AssemblyPath = "",
					IsDev = isDev,
					LoadError = $"Failed to parse plugin.json: {ex.Message}"
				};
			}
		}

		// Find the entry point DLL
		string? assemblyPath = null;
		if (manifest is not null && !string.IsNullOrWhiteSpace(manifest.EntryPoint))
		{
			string candidate = Path.Combine(directory, manifest.EntryPoint);
			if (File.Exists(candidate))
			{
				assemblyPath = candidate;
			}
			else
			{
				string debugCandidate = Path.Combine(directory, "bin", "Debug", "net10.0", manifest.EntryPoint);
				string releaseCandidate = Path.Combine(directory, "bin", "Release", "net10.0", manifest.EntryPoint);
				if (File.Exists(debugCandidate))
				{
					assemblyPath = debugCandidate;
				}
				else if (File.Exists(releaseCandidate))
				{
					assemblyPath = releaseCandidate;
				}
			}
		}

		if (assemblyPath is null)
		{
			// Search for DLL matching folder name, or any non-framework DLL in folder
			string folderName = Path.GetFileName(directory);
			string candidate = Path.Combine(directory, folderName + ".dll");
			if (File.Exists(candidate))
			{
				assemblyPath = candidate;
			}
			else
			{
				var dlls = Directory.GetFiles(directory, "*.dll")
					.Where(f => !Path.GetFileName(f).StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
							 && !Path.GetFileName(f).StartsWith("System.", StringComparison.OrdinalIgnoreCase))
					.ToList();

				assemblyPath = dlls.FirstOrDefault();
			}
		}

		if (assemblyPath is null || !File.Exists(assemblyPath))
		{
			return null;
		}

		PluginLoadContext? loadContext = null;
		try
		{
			loadContext = new PluginLoadContext(assemblyPath);
			var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

			var moduleType = assembly.GetExportedTypes()
				.FirstOrDefault(t => !t.IsAbstract && !t.IsInterface && typeof(IModule).IsAssignableFrom(t)
								 && t.GetConstructor(Type.EmptyTypes) != null);

			if (moduleType is null)
			{
				_logger.LogWarning("No public IModule implementation found in {AssemblyPath}", assemblyPath);
				return null;
			}

			var module = (IModule)Activator.CreateInstance(moduleType)!;

			manifest ??= new PluginManifest
			{
				Id = module.Descriptor.Id,
				Name = module.Descriptor.Name,
				Version = module.Descriptor.Version.ToString(),
				Description = module.Descriptor.Description ?? "",
				EntryPoint = Path.GetFileName(assemblyPath)
			};

			_logger.LogInformation("Loaded plugin {Name} ({Version}) from {Directory}", manifest.Name, manifest.Version, directory);

			return new PluginInfo
			{
				Manifest = manifest,
				DirectoryPath = directory,
				AssemblyPath = assemblyPath,
				Module = module,
				LoadContext = loadContext,
				IsDev = isDev
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to load plugin assembly from {AssemblyPath}", assemblyPath);

			manifest ??= new PluginManifest
			{
				Id = Guid.Empty,
				Name = Path.GetFileName(directory),
				Version = "0.0.0",
				EntryPoint = Path.GetFileName(assemblyPath)
			};

			return new PluginInfo
			{
				Manifest = manifest,
				DirectoryPath = directory,
				AssemblyPath = assemblyPath,
				LoadContext = loadContext,
				IsDev = isDev,
				LoadError = ex.Message
			};
		}
	}
}
