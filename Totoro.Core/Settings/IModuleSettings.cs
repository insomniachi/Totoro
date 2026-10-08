using System.Text.Json;
using Totoro.Core.Helpers;
using Totoro.Core.Module;

namespace Totoro.Core.Settings;

public interface IModuleSettings<out TData>
	where TData : DataContainerConfig
{
	TData Value { get; }
	Descriptor Descriptor { get; }
	void Save();
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

	public Descriptor Descriptor { get; }

	public void Save()
	{
		File.WriteAllText(_filePath, JsonSerializer.Serialize(Value, _options));
	}
}
