using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Module;
using Totoro.Core.Navigation;
using Totoro.Core.Settings;

namespace Totoro.Core.Extensions;

public static class ServiceCollectionExtensions
{
	extension (IServiceCollection services)
	{
		public IServiceCollection AddViewMap<TView, TViewModel>()
		 where TView : class, new()
		 where TViewModel : class
		{
			return services.AddViewMap(new ViewMap<TView, TViewModel>());
		}

		public IServiceCollection AddViewMap(ViewMap map)
		{
			services.AddTransient(_ => map);
			services.AddTransient(map.ViewModel);

			return services;
		}

		public IServiceCollection AddDataViewMap<TView, TViewModel, TData>()
			where TView : class, new()
			where TViewModel : class
			where TData : class
		{
			return services.AddViewMap(new DataViewMap<TView, TViewModel, TData>());
		}

		public IServiceCollection AddKeyedViewMap<TView, TViewModel>(string key)
			where TView : class, new()
			where TViewModel : class
		{
			return services.AddViewMap(new KeyedViewMap<TView, TViewModel>(key));
		}

		public IServiceCollection AddMainNavigationItem<TView, TViewModel>(string label, Action<NavigationItemViewModel>? configure = default)
			 where TView : class, new()
			 where TViewModel : class
		{
			services.AddKeyedViewMap<TView, TViewModel>(label);

			var item = new NavigationItemViewModel
			{
				Label = label,
				ViewModelType = typeof(TViewModel),
			};

			configure?.Invoke(item);

			services.AddTransient(_ => item);

			return services;
		}

		public IServiceCollection AddCoreServices()
		{
			return services.AddSingleton<IViewRegistry, ViewRegistry>()
						   .AddSingleton<IPreferences, Preferences>()
						   .AddLogging();
		}

		public IServiceCollection AddModuleSettings<T>(IModule<T> module) where T : DataContainerConfig, new()
		{
			services.AddTransient<IModuleSettings<T>>(_ => new ModuleSettings<T>(module.Descriptor))
					.AddKeyedTransient(module.Descriptor.Id, (sp, _) =>
					{
						var settings = sp.GetRequiredService<IModuleSettings<T>>();
						return settings.Value.ToDataContainer();
					});
			return services;
		}

		public IServiceCollection RegisterFactory<TService>(string key)
			   where TService : notnull
		{
			services.AddTransient<IFactory<TService, Guid>, Factory<TService, Guid>>(sp =>
			{
				// sp is the provider doing the resolving (the navigation scope for view models)
				var settings = sp.GetRequiredService<IPreferences>();
				return new Factory<TService, Guid>(sp, settings, key);
			});

			return services;
		}
	}
}
