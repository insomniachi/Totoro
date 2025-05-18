﻿using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Totoro.Plugins.Contracts;
using Totoro.Plugins.Options;

namespace Totoro.Core.Services;

public class LocalSettingsService : ILocalSettingsService
{
    private readonly JsonObject _settings = [];
    private readonly string _file;
    private readonly JsonSerializerOptions _options;
    public LocalSettingsService(IKnownFolders knownFolders)
    {
        _options = new JsonSerializerOptions()
        {
            WriteIndented = true,
            TypeInfoResolver = new TotoroTypeResolver(),
            Converters =
            {
                new JsonStringEnumConverter()
            },
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        _file = Path.Combine(knownFolders.ApplicationData, "LocalSettings.json");
        if (File.Exists(_file))
        {
            _settings = JsonNode.Parse(File.ReadAllText(_file)).AsObject();
        }
    }

    public T ReadSetting<T>(string key, T deafultValue = default)
    {
        if (!_settings.ContainsKey(key))
        {
            SaveSetting(key, deafultValue);
            return deafultValue;
        }

        return _settings[key].Deserialize<T>(_options);
    }

    public void RemoveSetting(string key)
    {
        _settings.Remove(key);
    }

    public void SaveSetting<T>(string key, T value)
    {
        _settings[key] = JsonNode.Parse(JsonSerializer.Serialize(value, _options));
        File.WriteAllText(_file, _settings.ToJsonString(_options));
    }
}

public class PluginConfiguration(ILocalSettingsService localSettingsService) : IPluginConfiguration
{
	public JsonObject GetConfiguration(Guid id)
	{
		throw new NotImplementedException();
	}

	public void Update()
	{
		throw new NotImplementedException();
	}
}

public class TotoroTypeResolver : DefaultJsonTypeInfoResolver
{
    private static readonly string[] _reactiveObjectIgnoredProperties = new[] { "Changing", "Changed", "ThrownExceptions" };

    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

        Type basePointType = typeof(PluginOption);
        if (jsonTypeInfo.Type == basePointType)
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType,
                DerivedTypes =
                {
                    new JsonDerivedType(typeof(SelectablePluginOption), "selectable"),
                }
            };
        }
        if (type.IsAssignableTo(typeof(ReactiveObject)))
        {
            jsonTypeInfo.Properties.RemoveAll(x => _reactiveObjectIgnoredProperties.Contains(x.Name));
        }

        return jsonTypeInfo;
    }
}

public static class ListHelpers
{
    public static void RemoveAll<T>(this IList<T> list, Predicate<T> predicate)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (predicate(list[i]))
            {
                list.RemoveAt(i--);
            }
        }
    }
}
