using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Totoro.Commands;
using Totoro.Core;
using Totoro.Core.Authentication;
using Totoro.Core.Dialogs;
using Totoro.Core.Extensions;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.Core.Module.Media;
using Totoro.Core.Module.Streaming;
using Totoro.Core.Navigation;
using Totoro.Core.Settings;
using Totoro.Playback;
using Totoro.UI;
using Totoro.UI.Authentication;
using Totoro.UI.Dialogs;
using Totoro.UI.Theming;
using Totoro.ViewModels;
using Totoro.Views;


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

				// A module provides its tracker and metadata provider as a pair, so one setting picks both.
				services.RegisterFactory<IAnimeTracker>(AnimeDatabasePreferences.Key);
				services.RegisterFactory<IAnimeMetadataProvider>(AnimeDatabasePreferences.Key);
				services.RegisterFactory<IAnimeProvider>("SelectedAnimeProvider");
				services.RegisterFactory<IMediaPlayer>("SelectedMediaPlayer");

				// Watching: the watch service drives a playback from a card's Play.
				services.AddSingleton<WatchService>();
				services.AddDialog<ShowPickerDialog, ShowPickerDialogViewModel>();

				services.AddCoreServices();
				services.AddSingleton<MainWindowViewModel>();
				services.AddSingleton<IAppearanceService>(sp => new AppearanceService(sp.GetRequiredService<IPreferences>(), () => _window));
				services.AddSingleton<IDialogService>(sp => new DialogService(
					sp.GetRequiredService<IViewRegistry>(),
					sp.GetRequiredService<IServiceScopeFactory>(),
					() => _window?.Content?.XamlRoot));
				services.AddSingleton<IWebAuthenticator>(_ => new WebAuthenticator(() => _window?.Content?.XamlRoot));
				services.AddDialog<TrackingDialog, TrackingDialogViewModel>();
				services.AddDialog<AnimeSettingsDialog, AnimeSettingsDialogViewModel>();
				services.AddDialog<DataContainerDialog, DataContainerDialogViewModel>();
				services.AddSingleton<AnimeCommands>();
				services.AddDataViewMap<AnimeDetailsPage, AnimeDetailsViewModel, AnimeModel>();
				services.AddMainNavigationItem<UserListPage,UserListViewModel>("Home", x =>
				{
					x.UnselectedIcon = Icons.Home;
					x.SelectedIcon = Icons.HomeFilled;
				});
				services.AddMainNavigationItem<AdvancedSearchPage, AdvancedSearchViewModel>("Search", x =>
				{
					x.UnselectedIcon = Icons.Search;
					x.SelectedIcon = Icons.Search;
				});
				services.AddMainNavigationItem<TestPage, TestViewModel>("Test", x =>
				{
					x.UnselectedIcon = Icons.Home;
					x.SelectedIcon = Icons.HomeFilled;
				});
				services.AddMainNavigationItem<SettingsPage, SettingsViewModel>("Settings", x =>
				{
					x.UnselectedIcon = Icons.Settings;
					x.SelectedIcon = Icons.Settings;
					x.IsFooter = true;
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
		_host.Services.GetRequiredService<IAppearanceService>().Apply();
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
			new Modules.AnimeDatabase.Anilist.AnilistModule(),
			new Modules.AnimeDatabase.MyAnimeList.MyAnimeListModule(),
			new Modules.AnimeProviders.SubsPlease.SubsPleaseModule(),
			new Modules.MediaPlayers.Mpv.MpvModule(),
			new Modules.MediaSegments.Aniskip.AniskipModule()
		];
	}
}
