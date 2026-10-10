using System.Text.Json;
using Totoro.Core.Helpers;
using Totoro.Core.Module;

namespace Totoro.Core.Settings;

/// <summary>A module's settings without their type, e.g. to list and edit every module's settings.</summary>
public interface IModuleSettings
{
	Descriptor Descriptor { get; }

	/// <summary>The settings object (the same one as <see cref="IModuleSettings{TData}.Value"/>).</summary>
	DataContainerConfig Config { get; }

	void Save();
}

public interface IModuleSettings<out TData> : IModuleSettings
	where TData : DataContainerConfig
{
	TData Value { get; }
}

public class ModuleSettings<TData> : IModuleSettings<TData>
	where TData : DataContainerConfig, new()
{
	private static readonly JsonSerializerOptions _options = new() { WriteIndented = true };
	private readonly string _filePath;

	public ModuleSettings(Descriptor descriptor)
	{
		Descriptor = descriptor;

		_filePath = FileHelper.GetModulePath(descriptor, "settings.json");

		Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
		Value = new TData();

		if (!File.Exists(_filePath))
		{
			return;
		}

		var text = File.ReadAllText(_filePath);
		if (JsonSerializer.Deserialize<TData>(text) is { } data)
		{
			Value = data;
		}
	}

	public TData Value { get; }

	public DataContainerConfig Config => Value;

	public Descriptor Descriptor { get; }

	public void Save()
	{
		File.WriteAllText(_filePath, JsonSerializer.Serialize(Value, _options));
	}
}
