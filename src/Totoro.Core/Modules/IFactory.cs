using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Settings;

namespace Totoro.Core.Modules;

/// <summary>
/// Resolves the module services registered under a module id. "Create" may hand back a shared singleton
/// (media players, torrent streamers), so callers must not dispose what they get.
/// </summary>
public interface IFactory<out TService>
	where TService : notnull
{
	TService? Create(Guid id);

	/// <summary>
	/// The service the user picked in Settings; when none is saved (or it was uninstalled),
	/// the first one registered, which follows module load order.
	/// </summary>
	TService? CreateDefault();

	IEnumerable<TService> CreateAll();
}

/// <summary>
/// Resolves services from the provider the factory itself was resolved from.
/// When a view model gets the factory inside a navigation scope, everything the factory
/// creates lives (and is disposed) with that page instead of with a throwaway scope.
/// </summary>
public class Factory<TService>(
	IServiceProvider serviceProvider,
	IPreferences preferences,
	string defaultKey) : IFactory<TService>
	where TService : notnull
{
	public TService? Create(Guid id) => serviceProvider.GetKeyedService<TService>(id);

	public TService? CreateDefault() =>
		Create(preferences.ReadKey<Guid>(defaultKey)) ?? CreateAll().FirstOrDefault();

	public IEnumerable<TService> CreateAll() => serviceProvider.GetKeyedServices<TService>(KeyedService.AnyKey);
}
