using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Totoro.Agent;
using Totoro.Commands;
using Totoro.Core;
using Totoro.Core.Agent;
using Totoro.Core.Authentication;
using Totoro.Core.Dialogs;
using Totoro.Core.Extensions;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Navigation;
using Totoro.Core.Relations;
using Totoro.Core.Settings;
using Totoro.Playback;
using Totoro.UI;
using Totoro.UI.Authentication;
using Totoro.UI.Dialogs;
using Totoro.UI.Theming;
using Totoro.UI.Threading;
using Totoro.ViewModels;
using Totoro.ViewModels.Relations;
using Totoro.ViewModels.Test;
using Totoro.Views;
using Totoro.Views.Relations;
using Totoro.Views.Test;


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
				services.RegisterFactory<ITorrentStreamer>("SelectedTorrentStreamer");

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

				// Everything the app does, as tools an AI agent can call (see Agent/). A new feature adds its tools here too.
				services.AddSingleton<IMainThread>(_ => new MainThread(() => _window?.DispatcherQueue));
				services.AddTransient<AnimeLookup>();
				services.AddAgentTools<AnimeTools>()
						.AddAgentTools<PlaybackTools>()
						.AddAgentTools<AnimeSettingsTools>()
						.AddAgentTools<AppSettingsTools>()
						.AddAgentTools<NavigationTools>()
						.AddAgentTools<RelationsTools>();

				services.AddDialog<TrackingDialog, TrackingDialogViewModel>();
				services.AddDialog<AnimeSettingsDialog, AnimeSettingsDialogViewModel>();
				// Episode numbering: anime-relations (a copy comes with the app, in Data) and the user's own rules.
				services.AddSingleton<IAnimeRelations>(sp => new AnimeRelations(
					Path.Combine(AppContext.BaseDirectory, "Data", "anime-relations.txt"), sp.GetRequiredService<ILogger<AnimeRelations>>()));
				services.AddViewMap<AnimeRelationsPage, AnimeRelationsViewModel>();
				services.AddDialog<RelationRuleDialog, RelationRuleDialogViewModel>();
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
					x.AnimatedIcon = Icons.SearchAnimated;
				});
				services.AddMainNavigationItem<TestPage, TestViewModel>("Test", x =>
				{
					// No filled beaker in Segoe Fluent Icons, so the same one either way.
					x.UnselectedIcon = Icons.Test;
					x.SelectedIcon = Icons.Test;
				});
				// The Test page's playgrounds, shown by its own navigator.
				services.AddKeyedViewMap<HtmlRendererPage, HtmlRendererViewModel>(TestViewModel.HtmlRendererKey);
				services.AddKeyedViewMap<SettingsEditorPage, SettingsEditorViewModel>(TestViewModel.SettingsEditorKey);
				services.AddKeyedViewMap<AgentToolsPage, AgentToolsViewModel>(TestViewModel.AgentToolsKey);
				services.AddMainNavigationItem<SettingsPage, SettingsViewModel>("Settings", x =>
				{
					x.AnimatedIcon = Icons.SettingsAnimated;
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
			new Modules.TorrentStreamers.Rqbit.RqbitModule(),
			new Modules.MediaSegments.Aniskip.AniskipModule()
		];
	}
}
