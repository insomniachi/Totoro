using Microsoft.Extensions.DependencyInjection;
using Totoro.Core.Modules;
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

		/// <summary>
		/// Registers a page shown for a piece of data, e.g. <c>INavigator.NavigateToDataAsync(anime)</c>. The navigator creates
		/// the view model itself and passes it the data; it isn't registered as a service, since DI validation (on in the
		/// Development environment) would fail on a constructor parameter that only the navigator can supply.
		/// </summary>
		public IServiceCollection AddDataViewMap<TView, TViewModel, TData>()
			where TView : class, new()
			where TViewModel : class
			where TData : class
		{
			return services.AddTransient<ViewMap>(_ => new DataViewMap<TView, TViewModel, TData>());
		}

		public IServiceCollection AddKeyedViewMap<TView, TViewModel>(string key)
			where TView : class, new()
			where TViewModel : class
		{
			return services.AddViewMap(new KeyedViewMap<TView, TViewModel>(key));
		}

		/// <summary>
		/// Registers a dialog: <typeparamref name="TView"/> is the dialog's content (it gets the view model through a
		/// <c>ViewModel</c> property, like pages do) and <typeparamref name="TViewModel"/> drives it.
		/// Show it with <see cref="Dialogs.IDialogService.ShowAsync{TViewModel, TResult}"/>.
		/// </summary>
		public IServiceCollection AddDialog<TView, TViewModel>()
			where TView : class, new()
			where TViewModel : class, Dialogs.IDialogViewModel
		{
			// Only the map, unlike AddViewMap: the dialog service creates the view model itself and passes it the
			// ShowAsync parameter. Registering it as a service would fail DI validation (on in the Development
			// environment) for view models whose constructor takes that parameter, e.g. an AnimeModel.
			return services.AddTransient<ViewMap>(_ => new ViewMap<TView, TViewModel>());
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
						   .AddSingleton<Modules.Media.IPlaybackProgressStore, Modules.Media.PlaybackProgressStore>()
							   .AddSingleton<IAnimeSettingsStore, AnimeSettingsStore>()
							   .AddSingleton<Services.AnimeTrackingService>()
							   .AddSingleton<Services.AnimeSongService>()
							   .AddSingleton<Services.ChatService>()
							   .AddSingleton<Services.UpdateService>()
							   .AddSingleton<Plugins.IPluginManager, Plugins.PluginManager>()
							   .AddSingleton<Agent.IAgentTools, Agent.AgentTools>()
						   .AddLogging();
		}

		public IServiceCollection AddModuleSettings<T>(IModule<T> module) where T : DataContainerConfig, new()
		{
			// One instance app-wide: each instance holds its own copy of the file, so a change saved through one (e.g.
			// signing in) would go unseen by the others.
			services.AddSingleton<IModuleSettings<T>>(_ => new ModuleSettings<T>(module.Descriptor))
					.AddKeyedTransient(module.Descriptor.Id, (sp, _) =>
					{
						var settings = sp.GetRequiredService<IModuleSettings<T>>();
						return settings.Value.ToDataContainer();
					});
			// Untyped, for listing and editing every module's settings; the same instance as above.
			services.AddSingleton<IModuleSettings>(sp => sp.GetRequiredService<IModuleSettings<T>>());
			return services;
		}

		/// <summary>
		/// Registers a class of agent tools: its methods marked <see cref="Agent.AgentToolAttribute"/> become tools of
		/// <see cref="Agent.IAgentTools"/>. Modules can add their own tools this way too.
		/// </summary>
		public IServiceCollection AddAgentTools<T>() where T : class
		{
			services.AddTransient<T>();
			services.AddSingleton(new Agent.AgentToolSet(typeof(T)));
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
