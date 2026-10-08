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


public class Factory<TService, TId>(
	IServiceScopeFactory serviceScopeFactory,
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

		using var scope = serviceScopeFactory.CreateScope();

		var service = scope.ServiceProvider.GetKeyedService<TService>(id);
		if (service is not null)
		{
			return service;
		}

		var defaultService = scope.ServiceProvider.GetKeyedService<TService>(default(TId)!);
		return defaultService;
	}


	public TService? CreateDefault()
	{
		using var scope = serviceScopeFactory.CreateScope();
		var key = preferences.ReadKey<TId>(defaultKey);
		return scope.ServiceProvider.GetKeyedService<TService>(key) ??
			   scope.ServiceProvider.GetKeyedServices<TService>(KeyedService.AnyKey).FirstOrDefault();
	}

	public TId? GetDefaultId() => preferences.ReadKey<TId>(defaultKey);

	public IEnumerable<TService> CreateAll()
	{
		using var scope = serviceScopeFactory.CreateScope();
		return scope.ServiceProvider.GetKeyedServices<TService>(KeyedService.AnyKey);
	}

	public bool CanCreate()
	{
		return CreateAll().Any();
	}
}