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

	/// <summary>The oldest version of the app the plugin works with.</summary>
	[JsonPropertyName("minTotoroVersion")]
	public string? MinAppVersion { get; init; }

	/// <summary>The package's SHA-256 (hex), checked before it's installed when the catalog gives one.</summary>
	[JsonPropertyName("sha256")]
	public string? Sha256 { get; init; }

	/// <summary>Whether this version of the app can run it.</summary>
	[JsonIgnore]
	public bool IsCompatible => AppInfo.IsAtLeast(MinAppVersion);

	[JsonIgnore]
	public bool IsInstalled { get; set; }

	[JsonIgnore]
	public bool HasUpdate { get; set; }

	[JsonIgnore]
	public string? InstalledVersion { get; set; }

	[JsonIgnore]
	public bool IsDev { get; set; }

	/// <summary>The loaded plugin's own icon file (its descriptor's Image), if it has one.</summary>
	[JsonIgnore]
	public string? LocalIcon { get; set; }

	/// <summary>Why the installed plugin didn't load, if it didn't.</summary>
	[JsonIgnore]
	public string? LoadError { get; set; }
}
