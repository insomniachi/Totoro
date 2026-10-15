using System.Text.Json.Serialization;

namespace Totoro.Core.Plugins;

public sealed record PluginCatalogItem
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

	[JsonPropertyName("downloadUrl")]
	public required string DownloadUrl { get; init; }

	[JsonPropertyName("moduleTypes")]
	public List<string> ModuleTypes { get; init; } = [];

	[JsonPropertyName("iconUrl")]
	public string? IconUrl { get; init; }

	[JsonPropertyName("repositoryUrl")]
	public string? RepositoryUrl { get; init; }

	[JsonIgnore]
	public bool IsInstalled { get; set; }

	[JsonIgnore]
	public bool HasUpdate { get; set; }

	[JsonIgnore]
	public string? InstalledVersion { get; set; }

	[JsonIgnore]
	public bool IsDev { get; set; }
}
