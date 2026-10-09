using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Settings;

namespace Totoro.Core.Module;

public interface IFactory<out TService, TId>
	where TService : notnull
{
	TService? Create(TId? id);

	TService? CreateDefault();

	IEnumerable<TService> CreateAll();

	TId? GetDefaultId();
}


/// <summary>
/// Resolves services from the provider the factory itself was resolved from.
/// When a view model gets the factory inside a navigation scope, everything the factory
/// creates lives (and is disposed) with that page instead of with a throwaway scope.
/// </summary>
public class Factory<TService, TId>(
	IServiceProvider serviceProvider,
	IPreferences preferences,
	string defaultKey) : IFactory<TService, TId>
	where TService : notnull
{
	public TService? Create(TId? id)
	{
		if (id is null)
		{
			return default;
		}

		var service = serviceProvider.GetKeyedService<TService>(id);
		if (service is not null)
		{
			return service;
		}

		return serviceProvider.GetKeyedService<TService>(default(TId)!);
	}


	public TService? CreateDefault()
	{
		var key = preferences.ReadKey<TId>(defaultKey);
		return serviceProvider.GetKeyedService<TService>(key) ??
			   serviceProvider.GetKeyedServices<TService>(KeyedService.AnyKey).FirstOrDefault();
	}

	public TId? GetDefaultId() => preferences.ReadKey<TId>(defaultKey);

	public IEnumerable<TService> CreateAll()
	{
		return serviceProvider.GetKeyedServices<TService>(KeyedService.AnyKey);
	}

	public bool CanCreate()
	{
		return CreateAll().Any();
	}
}