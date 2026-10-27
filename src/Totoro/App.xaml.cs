using CommunityToolkit.Mvvm.Messaging;
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
using Totoro.Core.Modules.Chat;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Navigation;
using Totoro.Core.Plugins;
using Totoro.Core.Relations;
using Totoro.Core.Services;
using Totoro.Core.Settings;
using Totoro.Core.Web;
using Totoro.Playback;
using Totoro.UI;
using Totoro.UI.Authentication;
using Totoro.UI.Dialogs;
using Totoro.UI.Theming;
using Totoro.UI.Threading;
using Totoro.UI.Web;
using WinUIEx;
using WebAuthenticator = Totoro.UI.Authentication.WebAuthenticator;
using Totoro.ViewModels;
using Totoro.ViewModels.Chat;
using Totoro.ViewModels.Relations;
using Totoro.Views;
using Totoro.Views.Relations;
using Totoro.Views.Settings;


namespace Totoro;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
	private Window? _window;
	private IHost _host;
	// Made before the host (its plugins add services), so with a logger of its own, to the debugger's output like the host's.
	private readonly IPluginLoader _pluginLoader = new PluginLoader(
		LoggerFactory.Create(x => x.AddDebug().SetMinimumLevel(LogLevel.Information)).CreateLogger<PluginLoader>());

	/// <summary>
	/// Restarts the app, e.g. to load plugins that were installed, with the arguments it was started with (so dev
	/// plugins, from --dev or --plugin-dir, are still loaded).
	/// </summary>
	public static void Restart()
	{
		var args = Environment.GetCommandLineArgs().Skip(1).Select(x => x.Contains(' ') ? $"\"{x}\"" : x);
		Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Join(' ', args));
	}

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
				services.RegisterFactory<IAnimeProvider>(PlaybackPreferences.AnimeProviderKey);
				services.RegisterFactory<IMediaPlayer>(PlaybackPreferences.MediaPlayerKey);
				services.RegisterFactory<ITorrentStreamer>(PlaybackPreferences.TorrentStreamerKey);
				services.RegisterFactory<IChatModel>("SelectedChatModel");

				// Watching: the watch service drives a playback from a card's Play.
				services.AddSingleton<WatchService>();
				// What the provider has of an anime's new episodes, for the cards' badges.
				services.AddSingleton<IProviderEpisodes, ProviderEpisodes>();
				services.AddSingleton<DownloadService>();
				services.AddSingleton<DiscordPresence>();
				services.AddSingleton<BrowserWatchTracker>();
				services.AddSingleton<AutoDownloader>();
				// Songs' audio, played in the Music tab.
				services.AddSingleton<ISongPlayer, SongAudioPlayer>();
				services.AddDialog<ShowPickerDialog, ShowPickerDialogViewModel>();

				services.AddCoreServices();
				services.AddSingleton<MainWindowViewModel>();
				services.AddSingleton<IAppearanceService>(sp => new AppearanceService(sp.GetRequiredService<IPreferences>(), () => _window));
				services.AddSingleton<IDialogService>(sp => new DialogService(
					sp.GetRequiredService<IViewRegistry>(),
					sp.GetRequiredService<IServiceScopeFactory>(),
					() => _window?.Content?.XamlRoot));
				services.AddSingleton<IWebAuthenticator>(_ => new WebAuthenticator(() => _window?.Content?.XamlRoot));
				// Pages from sites that only serve browsers (e.g. AnimePahe, behind Cloudflare).
				services.AddSingleton<IWebPageLoader>(sp => new WebPageLoader(
					() => _window is null ? throw new InvalidOperationException("The app's window isn't open yet.") : WinRT.Interop.WindowNative.GetWindowHandle(_window),
					() => _window?.Content?.XamlRoot,
					sp.GetRequiredService<IMainThread>()));

				// Everything the app does, as tools an AI agent can call (see Agent/). A new feature adds its tools here too.
				services.AddSingleton<IMainThread>(_ => new MainThread(() => _window?.DispatcherQueue));
				services.AddTransient<AnimeLookup>();
				services.AddAgentTools<AnimeTools>()
						.AddAgentTools<PlaybackTools>()
						.AddAgentTools<AnimeSettingsTools>()
						.AddAgentTools<AppSettingsTools>()
						.AddAgentTools<NavigationTools>()
						.AddAgentTools<RelationsTools>()
						.AddAgentTools<UpdateTools>()
						.AddAgentTools<AnimePluginTools>()
						.AddAgentTools<PluginTools>()
						.AddAgentTools<ReleaseScheduleTools>();
				// The assistant: a chat that calls those tools (see ChatService).
				services.AddSingleton<ChatViewModel>();

				services.AddDialog<TrackingDialog, TrackingDialogViewModel>();
				services.AddDialog<DownloadEpisodesDialog, DownloadEpisodesDialogViewModel>();
				services.AddDialog<DownloadInfoDialog, DownloadInfoDialogViewModel>();
				services.AddDialog<AnimeSettingsDialog, AnimeSettingsDialogViewModel>();
				// Episode numbering: anime-relations (downloaded when first needed) and the user's own rules.
				services.AddSingleton<IAnimeRelations, AnimeRelations>();
				services.AddViewMap<AnimeRelationsPage, AnimeRelationsViewModel>();
				services.AddDialog<RelationRuleDialog, RelationRuleDialogViewModel>();
				services.AddDialog<DataContainerDialog, DataContainerDialogViewModel>();
				services.AddSingleton<AnimeCommands>();
				services.AddDataViewMap<AnimeDetailsPage, AnimeDetailsViewModel, AnimeModel>();
				// First, so it's where the app starts until the user picks another startup page (see StartupPagePreferences).
				services.AddMainNavigationItem<DiscoverPage, DiscoverViewModel>("Discover", x =>
				{
					x.AnimatedIcon = Icons.DiscoverAnimated;
				});
				services.AddMainNavigationItem<UserListPage, UserListViewModel>("Library", x =>
				{
					x.AnimatedIcon = Icons.LibraryAnimated;
				});
				services.AddMainNavigationItem<AdvancedSearchPage, AdvancedSearchViewModel>("Search", x =>
				{
					x.AnimatedIcon = Icons.SearchAnimated;
				});
				services.AddMainNavigationItem<ReleaseSchedulePage, ReleaseScheduleViewModel>("Schedule", x =>
				{
					x.AnimatedIcon = Icons.ScheduleAnimated;
				});
				services.AddMainNavigationItem<ProviderWatchPage, ProviderWatchViewModel>("Watch", x =>
				{
					x.AnimatedIcon = Icons.PlayAnimated;
				});
				services.AddMainNavigationItem<DownloadsPage, DownloadsViewModel>("Downloads", x =>
				{
					x.AnimatedIcon = Icons.DownloadAnimated;
				});
				services.AddMainNavigationItem<PluginsPage, PluginsViewModel>("Plugins", x =>
				{
					x.AnimatedIcon = Icons.PluginsAnimated;
				});
#if DEBUG
				// A developer's playground: not for users.
				services.AddMainNavigationItem<AgentToolsPage, AgentToolsViewModel>("Agent tools", x =>
				{
					x.AnimatedIcon = Icons.AgentToolsAnimated;
				});
#endif
				services.AddViewMap<PluginTesterPage, PluginTesterViewModel>();
				services.AddMainNavigationItem<SettingsPage, SettingsViewModel>("Settings", x =>
				{
					x.AnimatedIcon = Icons.SettingsAnimated;
					x.IsFooter = true;
				});
				// Settings' categories, after it: the navigator finds the first page registered for SettingsViewModel.
				services.AddKeyedViewMap<GeneralSettingsPage, SettingsViewModel>(SettingsViewModel.GeneralKey);
				services.AddKeyedViewMap<AccountsSettingsPage, SettingsViewModel>(SettingsViewModel.AccountsKey);
				services.AddKeyedViewMap<PlaybackSettingsPage, SettingsViewModel>(SettingsViewModel.PlaybackKey);
				services.AddKeyedViewMap<DownloadsSettingsPage, SettingsViewModel>(SettingsViewModel.DownloadsKey);
				services.AddKeyedViewMap<AboutSettingsPage, SettingsViewModel>(SettingsViewModel.AboutKey);
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
		// Follows playback from now on.
		_host.Services.GetRequiredService<DiscordPresence>();
		// Marks episodes finished in a browser watched, while the app's open.
		_host.Services.GetRequiredService<BrowserWatchTracker>();
		// Downloads the next episode of anime being watched, if that's turned on.
		_host.Services.GetRequiredService<AutoDownloader>();
		// The Downloads item in the navigation shows how many downloads are going.
		var downloads = _host.Services.GetRequiredService<DownloadService>();
		var downloadsItem = _host.Services.GetServices<NavigationItemViewModel>().First(x => x.ViewModelType == typeof(DownloadsViewModel));
		downloads.PropertyChanged += (_, _) => downloadsItem.BadgeCount = downloads.ActiveCount;
		var window = _window;
		var preferences = _host.Services.GetRequiredService<IPreferences>();
		var manager = WinUIEx.WindowManager.Get(window);
		var shown = false;
		var quitting = false;

		void Show()
		{
			if (manager.WindowState == WinUIEx.WindowState.Minimized)
			{
				window.Restore();
			}
			else
			{
				window.Show();
			}

			window.Activate();
			// Once activated: maximized before its first activation from the tray, the content kept its old size, unpainted.
			if (!shown && preferences.StartMaximized)
			{
				window.Maximize();
			}

			shown = true;
			window.SetForegroundWindow();
		}

		// Its icon is there while the window's hidden in it, and while closing the window doesn't quit, to quit from.
		void UpdateTray() => manager.IsVisibleInTray = !window.AppWindow.IsVisible || preferences.CloseAction != WindowCloseAction.Quit;

		// Quitting while episodes download asks first, and cancels them (deleting what they had) if the user goes on.
		async Task QuitAsync()
		{
			if (downloads.ActiveCount > 0)
			{
				Show();
				if (window.Content is FrameworkElement { XamlRoot: null } root)
				{
					// Never shown before (it started in the tray): the dialog needs its content loaded.
					var loaded = new TaskCompletionSource();
					root.Loaded += (_, _) => loaded.TrySetResult();
					await loaded.Task;
				}

				var count = downloads.ActiveCount;
				var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
				{
					XamlRoot = window.Content.XamlRoot,
					Style = Resources["DefaultContentDialogStyle"] as Style,
					Title = count == 1 ? "An episode is downloading" : $"{count} episodes are downloading",
					Content = "Quitting Totoro cancels them, and deletes what they've downloaded so far.",
					PrimaryButtonText = "Quit anyway",
					CloseButtonText = "Keep downloading",
					DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
				};
				try
				{
					if (await dialog.ShowAsync() != Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
					{
						return;
					}
				}
				catch (System.Runtime.InteropServices.COMException)
				{
					// Another dialog is open (only one shows at a time): Totoro keeps running until that's closed.
					return;
				}

				await downloads.CancelAllAsync(TimeSpan.FromSeconds(10));
			}

			quitting = true;
			window.Close();
		}

		window.AppWindow.Closing += (sender, args) =>
		{
			if (quitting)
			{
				return;
			}

			args.Cancel = true;
			switch (preferences.CloseAction)
			{
				case WindowCloseAction.Minimize:
					window.Minimize();
					break;
				case WindowCloseAction.Tray:
					window.Hide();
					break;
				default:
					_ = QuitAsync();
					break;
			}

			UpdateTray();
		};
		window.Activated += (_, _) => UpdateTray();
		manager.TrayIconSelected += (_, _) => Show();
		manager.TrayIconContextMenu += (sender, e) =>
		{
			var open = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Open Totoro" };
			open.Click += (_, _) => Show();
			var quit = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Quit Totoro" };
			quit.Click += (_, _) => _ = QuitAsync();
			e.Flyout = new Microsoft.UI.Xaml.Controls.MenuFlyout { Items = { open, quit } };
		};
		// The app ends with its main window, the built-in player's window too.
		window.Closed += (_, _) =>
		{
			manager.IsVisibleInTray = false;
			Exit();
		};
		// E.g. for the installer of a newer version to replace the app (see UpdateService).
		WeakReferenceMessenger.Default.Register<App, AppExitRequestedMessage>(this, (_, _) =>
		{
			quitting = true;
			window.Close();
		});
		if (preferences.StartInTray)
		{
			manager.IsVisibleInTray = true;
		}
		else
		{
			Show();
		}
	}

	private void AddModules(IServiceCollection services)
	{
		var devPaths = GetDevPluginPaths();
		var loaded = _pluginLoader.LoadPlugins(devPaths);

		services.AddSingleton<IPluginLoader>(_pluginLoader);

		foreach (var module in GetInBuiltModules())
		{
			module.ConfigureServices(services);
		}

		// A plugin that throws here (a bug, or built against another version of Totoro.Core) is left out rather than
		// stopping the app from starting, which would leave no way to remove it. What it registered before throwing is
		// taken out again (it only adds, e.g. TryAdd in AddHttpClient), and the Plugins page shows the error.
		foreach (var plugin in loaded.Where(p => p.IsLoaded))
		{
			var count = services.Count;
			try
			{
				plugin.Module!.ConfigureServices(services);
			}
			catch (Exception ex)
			{
				while (services.Count > count)
				{
					services.RemoveAt(services.Count - 1);
				}

				plugin.LoadError = $"Failed to set up: {ex.Message}";
			}
		}

		// After the plugins: with no provider selected, the first registered is used, and that should be one to stream from.
		new Modules.AnimeProviders.Downloads.DownloadsModule().ConfigureServices(services);
	}

	private static List<string> GetDevPluginPaths()
	{
		var paths = new List<string>();
		var args = Environment.GetCommandLineArgs();

		// Dev mode is OFF by default. Only load external dev directories if explicitly requested via CLI:
		// e.g. --dev, --enable-dev-plugins, or direct path via --plugin-dir / -p.
		bool isDevExplicitlyEnabled = args.Any(a =>
			a.Equals("--dev", StringComparison.OrdinalIgnoreCase) ||
			a.Equals("--enable-dev", StringComparison.OrdinalIgnoreCase) ||
			a.Equals("--enable-dev-plugins", StringComparison.OrdinalIgnoreCase) ||
			a.Equals("--plugin-dir", StringComparison.OrdinalIgnoreCase) ||
			a.Equals("-p", StringComparison.OrdinalIgnoreCase) ||
			a.Equals("--dev-plugin", StringComparison.OrdinalIgnoreCase));

		if (!isDevExplicitlyEnabled)
		{
			// Safe default: regular users only load verified installed plugins from %LocalAppData%\Totoro\Plugins
			return paths;
		}

		// Folders in TOTORO_PLUGIN_DIRS (or TOTORO_DEV_PLUGIN_DIR), separated by ';'.
		var envDirs = Environment.GetEnvironmentVariable("TOTORO_PLUGIN_DIRS")
					  ?? Environment.GetEnvironmentVariable("TOTORO_DEV_PLUGIN_DIR");
		if (!string.IsNullOrWhiteSpace(envDirs))
		{
			paths.AddRange(envDirs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
		}

		for (int i = 1; i < args.Length; i++)
		{
			if ((args[i].Equals("--plugin-dir", StringComparison.OrdinalIgnoreCase) ||
				 args[i].Equals("-p", StringComparison.OrdinalIgnoreCase) ||
				 args[i].Equals("--dev-plugin", StringComparison.OrdinalIgnoreCase)) &&
				i + 1 < args.Length)
			{
				paths.Add(args[i + 1]);
				i++;
			}
		}

		bool scanDevDirectories = args.Any(a =>
			a.Equals("--dev", StringComparison.OrdinalIgnoreCase) ||
			a.Equals("--enable-dev", StringComparison.OrdinalIgnoreCase) ||
			a.Equals("--enable-dev-plugins", StringComparison.OrdinalIgnoreCase));

		if (scanDevDirectories)
		{
			// When explicitly running with --dev in repository, discover plugins in local plugins/ directory or sibling Totoro.Plugins repo
			try
			{
				var dir = AppContext.BaseDirectory;
				while (!string.IsNullOrEmpty(dir))
				{
					if (File.Exists(Path.Combine(dir, "Totoro.slnx")) || Directory.Exists(Path.Combine(dir, ".git")))
					{
						var localPlugins = Path.Combine(dir, "plugins");
						if (Directory.Exists(localPlugins))
						{
							paths.Add(localPlugins);
						}

						var siblingPlugins = Path.GetFullPath(Path.Combine(dir, "..", "Totoro.Plugins"));
						if (Directory.Exists(siblingPlugins))
						{
							paths.Add(siblingPlugins);
						}

						break;
					}
					dir = Path.GetDirectoryName(dir);
				}
			}
			catch
			{
			}
		}

		return paths;
	}

	private static List<IModule> GetInBuiltModules()
	{
		return
		[
			new Modules.AnimeDatabase.Anilist.AnilistModule(),
			new Modules.AnimeDatabase.MyAnimeList.MyAnimeListModule(),
			new Modules.MediaPlayers.Mpv.MpvModule(),
			new Modules.MediaPlayers.Vlc.VlcModule(),
			new Modules.TorrentStreamers.Rqbit.RqbitModule(),
			new Modules.MediaSegments.Aniskip.AniskipModule(),
			new Modules.ChatModels.Ollama.OllamaModule()
		];
	}
}
