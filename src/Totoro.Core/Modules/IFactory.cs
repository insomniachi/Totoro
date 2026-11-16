using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Settings;

namespace Totoro.Core.Modules;

/// <summary>
/// Resolves the module services registered under an id. "Create" may hand back a shared singleton
/// (media players, torrent streamers), so callers must not dispose what they get.
/// </summary>
public interface IFactory<out TService, TId>
	where TService : notnull
{
	TService? Create(TId id);

	TService? CreateDefault();

	IEnumerable<TService> CreateAll();
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
	public TService? Create(TId id) => serviceProvider.GetKeyedService<TService>(id);

	public TService? CreateDefault()
	{
		var key = preferences.ReadKey<TId>(defaultKey);
		return serviceProvider.GetKeyedService<TService>(key) ??
			   serviceProvider.GetKeyedServices<TService>(KeyedService.AnyKey).FirstOrDefault();
	}

	public IEnumerable<TService> CreateAll() => serviceProvider.GetKeyedServices<TService>(KeyedService.AnyKey);
}
