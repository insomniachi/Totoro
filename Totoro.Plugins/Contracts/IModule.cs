using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace Totoro.Plugins.Contracts;

public interface IModule
{
	void RegisterServices(IServiceCollection services);
	void OnServiceProviderCreated(IServiceProvider serviceProvider);
	void RegisterPlugins(IPluginRegistrar registrar);
}

public class PluginDescriptor
{
	public required Guid Id { get; init; }
	public required string DisplayName { get; init; }
	public required PluginType Type { get; init; }
	public required Version Version { get; init; }
	public string? Description { get; init; }
	public Stream? Icon { get; init; }
}

public interface IPluginCollection
{
	IEnumerable<PluginDescriptor> GetAll();
	void Add(PluginDescriptor descriptor);
}

public enum PluginType
{
	Anime,
	TorrentIndexer,
	MediaEngine
}

public interface IPluginRegistrar
{
	void Register(PluginDescriptor descriptor);
}

public interface IPluginOptions
{
	void Initialize(Guid id);
}

public abstract class PluginConfiguration<T>(IPluginConfiguration pluginConfig) : INotifyPropertyChanged, IPluginOptions
	where T : class
{
	protected readonly IPluginConfiguration _pluginConfig = pluginConfig;
	private JsonObject? _backingConfiguration;

	public event PropertyChangedEventHandler? PropertyChanged;

	protected abstract void Load(T options);

	protected abstract T CreateDefault();

	public void Initialize(Guid pluginId)
	{
		_backingConfiguration = _pluginConfig.GetConfiguration(pluginId);
		var options = _backingConfiguration.Deserialize<T>() ?? CreateDefault();
		Load(options);
	}

	protected void SetValue<V>(ref V field, V value, [CallerMemberName] string propertyName = "")
	{
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		if(_backingConfiguration is { } config)
		{
			config[propertyName] = JsonNode.Parse(JsonSerializer.Serialize(value));
			_pluginConfig.Update();
		}
	}
}

public interface IPluginConfiguration
{
	JsonObject GetConfiguration(Guid id);
	void Update();
}

public abstract class ModuleBase : IModule
{
	public void OnServiceProviderCreated(IServiceProvider serviceProvider)
	{
		foreach (var plugin in GetPlugins())
		{
			var options = serviceProvider.GetKeyedService<IPluginOptions>(plugin.Id);

			if(options is null)
			{
				return;
			}

			options.Initialize(plugin.Id);
		}
	}

	public void RegisterPlugins(IPluginRegistrar registrar)
	{
		foreach (var descriptor in GetPlugins())
		{
			registrar.Register(descriptor);
		}
	}

	public abstract void RegisterServices(IServiceCollection services);

	protected abstract IEnumerable<PluginDescriptor> GetPlugins();
}

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddPluginOption<T>(this IServiceCollection services, Guid id)
		where T : class, IPluginOptions
	{
		services.AddKeyedSingleton<IPluginOptions,T>(id);
		services.AddSingleton<T>(sp => (T)sp.GetRequiredKeyedService<IPluginOptions>(id));

		return services;
	}
}