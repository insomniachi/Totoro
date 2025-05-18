using Microsoft.Extensions.DependencyInjection;
using Totoro.Plugins.Contracts;

namespace Totoro.Plugins;

internal class PluginRegistrar(IServiceScopeFactory serviceScopeFactory) : IPluginRegistrar
{
	public void Register(PluginDescriptor descriptor)
	{
		using var scope = serviceScopeFactory.CreateScope();
		var collection = scope.ServiceProvider.GetRequiredKeyedService<IPluginCollection>(descriptor.Type);
		collection.Add(descriptor);
	}
}

internal class PluginCollection : IPluginCollection
{
	private readonly List<PluginDescriptor> _plugins = [];

	public IEnumerable<PluginDescriptor> GetAll() => _plugins;
	
	public void Add(PluginDescriptor descriptor)
	{
		if (_plugins.Any(x => x.Id == descriptor.Id))
		{
			throw new ArgumentException($"Unable to register {descriptor.DisplayName}. Plugin with ID {descriptor.Id} already exists.");
		}

		_plugins.Add(descriptor);
	}
}


public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddPluginRegistrar(this IServiceCollection services)
	{
		services.AddSingleton<IPluginRegistrar, PluginRegistrar>();

		foreach (var type in Enum.GetValues<PluginType>())
		{
			services.AddKeyedSingleton<IPluginCollection, PluginCollection>(type);
		}

		return services;
	}
}