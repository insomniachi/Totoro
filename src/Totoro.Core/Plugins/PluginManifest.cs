using System.Text.Json.Serialization;

namespace Totoro.Core.Plugins;

/// <summary>
/// Manifest metadata describing a plugin, loaded from plugin.json in the plugin directory.
/// </summary>
public sealed record PluginManifest
{
	[JsonPropertyName("id")]
	public required Guid Id { get; init; }

	[JsonPropertyName("name")]
	public required string Name { get; init; }

	[JsonPropertyName("version")]
	public required string Version { get; init; }

	[JsonPropertyName("description")]
	public string Description { get; init; } = "";

	[JsonPropertyName("author")]
	public string Author { get; init; } = "";

	/// <summary>
	/// The main assembly file name (e.g. "Totoro.Plugins.Senshi.dll").
	/// </summary>
	[JsonPropertyName("entryPoint")]
	public required string EntryPoint { get; init; }

	/// <summary>
	/// The module kinds provided (e.g. ["AnimeProvider", "ChatModel"]).
	/// </summary>
	[JsonPropertyName("moduleTypes")]
	public List<string> ModuleTypes { get; init; } = [];

	[JsonPropertyName("minAppVersion")]
	public string MinAppVersion { get; init; } = "2.0.0";

	[JsonPropertyName("iconUrl")]
	public string? IconUrl { get; init; }

	[JsonPropertyName("downloadUrl")]
	public string? DownloadUrl { get; init; }

	[JsonPropertyName("updateUrl")]
	public string? UpdateUrl { get; init; }
}
