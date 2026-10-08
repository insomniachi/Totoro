using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Totoro.Core.Settings;

public interface IPreferences
{
	T? ReadKey<T>(string key, T? defaultValue = default);

	void SaveKey<T>(string key, T value);

	void RemoveKey(string key);
}

internal class Preferences : IPreferences
{
	private readonly string _file;
	private readonly JsonSerializerOptions _options;
	private readonly JsonObject _settings = [];

	public Preferences()
	{
		_options = new JsonSerializerOptions
		{
			WriteIndented = true,
			Converters =
			{
				new JsonStringEnumConverter()
			},
			NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
		};

		_file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Totoro", "settings.json");
		if (File.Exists(_file))
		{
			_settings = JsonNode.Parse(File.ReadAllText(_file))!.AsObject();
		}
	}

	public T? ReadKey<T>(string key, T? deafultValue = default)
	{
		if (_settings.ContainsKey(key))
		{
			return _settings[key].Deserialize<T>(_options);
		}

		SaveKey(key, deafultValue);
		return deafultValue;
	}

	public void RemoveKey(string key)
	{
		_settings.Remove(key);
	}

	public void SaveKey<T>(string key, T value)
	{
		_settings[key] = JsonNode.Parse(JsonSerializer.Serialize(value, _options));
		File.WriteAllText(_file, _settings.ToJsonString(_options));
	}
}
