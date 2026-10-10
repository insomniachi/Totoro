using System.Text.Json;
using System.Text.Json.Nodes;
using Totoro.Core.Helpers;
using Totoro.Core.Module;

namespace Totoro.Core.Settings;

/// <summary>A module's settings without their type, e.g. to list and edit every module's settings.</summary>
public interface IModuleSettings
{
	Descriptor Descriptor { get; }

	/// <summary>The settings object (the same one as <see cref="IModuleSettings{TData}.Value"/>).</summary>
	DataContainerConfig Config { get; }

	/// <summary>
	/// A copy of the settings to use without changing them, e.g. for one anime: with <paramref name="changes"/> (a
	/// settings object as JSON, from <see cref="ToJson"/>) over them, if given. Properties the JSON lacks keep their
	/// value here.
	/// </summary>
	DataContainerConfig CreateCopy(JsonObject? changes = null);

	/// <summary>A settings object (of this module's type, e.g. from <see cref="CreateCopy"/>) as JSON, to keep.</summary>
	JsonObject ToJson(DataContainerConfig config);

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
	private static readonly JsonSerializerOptions s_options = new() { WriteIndented = true };
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

	public DataContainerConfig CreateCopy(JsonObject? changes = null)
	{
		var json = ToJson(Value);
		foreach (var (name, value) in changes ?? [])
		{
			json[name] = value?.DeepClone();
		}

		return json.Deserialize<TData>(s_options) ?? new TData();
	}

	public JsonObject ToJson(DataContainerConfig config)
	{
		return JsonSerializer.SerializeToNode(config, config.GetType(), s_options)!.AsObject();
	}

	public Descriptor Descriptor { get; }

	public void Save()
	{
		File.WriteAllText(_filePath, JsonSerializer.Serialize(Value, s_options));
	}
}
