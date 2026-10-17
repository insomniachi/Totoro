using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Totoro.Core.Helpers;
using Totoro.Core.Modules;

namespace Totoro.Core.Plugins;

public sealed class PluginLoader(ILogger<PluginLoader>? logger = null) : IPluginLoader
{
	/// <summary>The suffix of a plugin's folder holding its update, staged while the plugin was loaded.</summary>
	public const string PendingSuffix = ".pending";

	/// <summary>A file marking a plugin's folder for deletion at the next start, when it couldn't be deleted at once.</summary>
	public const string DeleteMarker = ".delete";

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

		// 2. Standard app data plugins folder: %LocalAppData%\Totoro\Plugins
		var standardPluginsDir = FileHelper.GetPath("Plugins");
		if (Directory.Exists(standardPluginsDir))
		{
			ApplyPendingInstalls(standardPluginsDir);
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
						if (subDir.EndsWith(PendingSuffix, StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}

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

			// Failed ones too (if their id is known): one plugin in two places is listed once, whichever loaded first.
			if (pluginInfo.Id != Guid.Empty && !seenPluginIds.Add(pluginInfo.Id))
			{
				_logger.LogWarning("Duplicate plugin ID {Id} detected in {Directory}; ignoring duplicate.", pluginInfo.Id, pluginDir);
				continue;
			}

			_plugins.Add(pluginInfo);
		}

		return _plugins;
	}

	/// <summary>
	/// Swaps in the updates PluginManager staged while their plugins were loaded (and so locked): before anything is
	/// loaded, nothing holds the old folders.
	/// </summary>
	private void ApplyPendingInstalls(string pluginsDir)
	{
		foreach (var pending in Directory.GetDirectories(pluginsDir, "*" + PendingSuffix))
		{
			var target = pending[..^PendingSuffix.Length];
			try
			{
				if (Directory.Exists(target))
				{
					Directory.Delete(target, recursive: true);
				}

				Directory.Move(pending, target);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to apply the staged plugin update {Directory}", pending);
			}
		}
	}

	private static bool IsPluginDirectory(string dir)
	{
		return File.Exists(Path.Combine(dir, "plugin.json"))
			|| Directory.GetFiles(dir, "*.deps.json").Length > 0;
	}

	private PluginInfo? LoadPluginFromDirectory(string directory, bool isDev)
	{
		if (File.Exists(Path.Combine(directory, DeleteMarker)))
		{
			try
			{
				Directory.Delete(directory, recursive: true);
			}
			catch (Exception ex)
			{
				// Still uninstalled: not loaded, and tried again at the next start.
				_logger.LogWarning(ex, "Failed to delete plugin marked for removal: {Directory}", directory);
			}

			return null;
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
			// The DLL named after the folder, else the one the build's deps.json is for (X.deps.json goes with X.dll): the
			// folder holds the plugin's dependencies too (Flurl...), so any other DLL may not be the plugin.
			string folderName = Path.GetFileName(directory);
			string candidate = Path.Combine(directory, folderName + ".dll");
			if (File.Exists(candidate))
			{
				assemblyPath = candidate;
			}
			else
			{
				var fromDeps = Directory.GetFiles(directory, "*.deps.json")
					.Select(f => Path.Combine(directory, Path.GetFileName(f)[..^".deps.json".Length] + ".dll"))
					.Where(File.Exists)
					.ToList();
				if (fromDeps.Count == 1)
				{
					assemblyPath = fromDeps[0];
				}
				else
				{
					// No way to tell, unless there's only the one.
					var dlls = Directory.GetFiles(directory, "*.dll")
						.Where(f => !Path.GetFileName(f).StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
								 && !Path.GetFileName(f).StartsWith("System.", StringComparison.OrdinalIgnoreCase))
						.ToList();
					assemblyPath = dlls.Count == 1 ? dlls[0] : null;
					if (dlls.Count > 1)
					{
						_logger.LogWarning("Can't tell which DLL is the plugin in {Directory}: add an entryPoint to its plugin.json.", directory);
					}
				}
			}
		}

		if (assemblyPath is null || !File.Exists(assemblyPath))
		{
			return null;
		}

		// Built against a later Totoro.Core, it would fail with missing types or methods, perhaps only once used.
		if (manifest is not null && !AppInfo.IsAtLeast(manifest.MinAppVersion))
		{
			return new PluginInfo
			{
				Manifest = manifest,
				DirectoryPath = directory,
				AssemblyPath = assemblyPath,
				IsDev = isDev,
				LoadError = $"Needs Totoro {manifest.MinAppVersion} or later (this is {AppInfo.Version}).",
			};
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
