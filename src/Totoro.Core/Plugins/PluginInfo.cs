using Totoro.Core.Modules;

namespace Totoro.Core.Plugins;

/// <summary>
/// Status and metadata for a discovered or loaded plugin.
/// </summary>
public sealed class PluginInfo
{
	public required PluginManifest Manifest { get; init; }
	public required string DirectoryPath { get; init; }
	public required string AssemblyPath { get; init; }
	public IModule? Module { get; set; }
	public PluginLoadContext? LoadContext { get; set; }
	public bool IsDev { get; init; }
	public string? LoadError { get; set; }

	public bool IsLoaded => Module is not null && LoadError is null;
	public Guid Id => Manifest.Id;
	public string Name => Manifest.Name;
	public string Version => Manifest.Version;
	public string Description => Manifest.Description;
}
