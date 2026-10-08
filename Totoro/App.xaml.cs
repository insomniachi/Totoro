using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Totoro.Core;
using Totoro.Core.Extensions;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.UI;
using Totoro.ViewModels;
using Totoro.Views;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace Totoro;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
	private Window? _window;
	private IHost _host;

	public App()
	{
		InitializeComponent();
		_host = Host.CreateDefaultBuilder()
			.ConfigureServices((context, services) =>
			{
				AddModules(services);

				services.RegisterFactory<IAnimeTracker>("SelectedTracker");

				services.AddCoreServices();
				services.AddSingleton<MainWindowViewModel>();
				services.AddMainNavigationItem<UserListPage,UserListViewModel>("Home", x =>
				{
					x.UnselectedIcon = Icons.Home;
					x.SelectedIcon = Icons.HomeFilled;
				});
			})
			.Build();
	}

	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		Locator.SetServiceProvider(_host.Services);
		_window = new MainWindow()
		{
			ViewModel = _host.Services.GetRequiredService<MainWindowViewModel>(),
		};
		_window.Activate();
	}

	private void AddModules(IServiceCollection services)
	{
		List<IModule> modules =
		[
			..GetInBuiltModules(),
				];
		foreach (var module in modules)
		{
			module.ConfigureServices(services);
		}
	}

	private static List<IModule> GetInBuiltModules()
	{
		return
		[
			new Modules.AnimeDatabase.Anilist.AnilistModule()
		];
	}
}
