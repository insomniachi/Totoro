using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Serilog;
using Totoro.Core;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Media;
using Totoro.Core.Modules.Streaming;
using Totoro.Core.Navigation;
using Totoro.Core.Plugins;
using Totoro.Core.Services;
using Totoro.Core.Settings;
using Totoro.Playback;
using Totoro.UI.Dialogs;
using WinUIEx;
using Totoro.ViewModels;
using Totoro.Setup;
#if DEBUG
// For the agent tools' HTTP server (see OnLaunched).
using Totoro.Agent;
using Totoro.Core.Agent;
#endif


namespace Totoro;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
	private Window? _window;
	// Built in OnLaunched, after the setup wizard's plugin steps, so the plugins it installs load without a restart.
	private IHost _host = null!;
	// Before anything reads a setting (which writes the file): no settings yet means a fresh install, which gets the setup wizard.
	private readonly bool _firstRun = !File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Totoro", "settings.json"));
	// A file a day in %LocalAppData%\Totoro\Logs, the last week's kept. Shared by the host and the plugin loader's own
	// logger, so one writer appends to the file.
	private static readonly Serilog.Core.Logger s_fileLogs = new Serilog.LoggerConfiguration()
		.WriteTo.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Totoro", "Logs", "totoro-.log"),
			rollingInterval: Serilog.RollingInterval.Day, retainedFileCountLimit: 7)
		.CreateLogger();
	// Made before the host (its plugins add services), so with a logger of its own, to the same places as the host's.
	private readonly IPluginLoader _pluginLoader = new PluginLoader(
		LoggerFactory.Create(x => x.AddDebug().AddSerilog(s_fileLogs).SetMinimumLevel(LogLevel.Information)).CreateLogger<PluginLoader>());

	/// <summary>
	/// Restarts the app, e.g. to load plugins that were installed, with the arguments it was started with (so dev
	/// plugins, from --dev or --plugin-dir, are still loaded).
	/// </summary>
	[System.Runtime.InteropServices.LibraryImport("user32.dll")]
	[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
	private static partial bool IsIconic(nint window);

	[System.Runtime.InteropServices.LibraryImport("user32.dll")]
	[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
	private static partial bool ShowWindow(nint window, int command);

	public static void Restart()
	{
		// Not --startup: the user asked for the restart, so it opens the window.
		var args = Environment.GetCommandLineArgs().Skip(1)
			.Where(x => !x.Equals(Helpers.WindowsStartup.Argument, StringComparison.OrdinalIgnoreCase))
			.Select(x => x.Contains(' ') ? $"\"{x}\"" : x);
		Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Join(' ', args));
	}

	// Notifications pop up only for an app ID a Start menu shortcut carries (the installer gives Totoro's this one); the
	// Windows App SDK's own, made up for unpackaged apps, only reach the notification centre on newer Windows.
	private const string AppUserModelId = "Totoro";

	// The ID Windows starts Totoro by for a click on a notification, fixed so the installer's shortcut can carry it too:
	// Windows sends the click to the one on the shortcut. The Windows App SDK keeps one already registered, else makes one up.
	private const string ToastActivator = "{DB818E89-7953-4806-AA3D-DD0DE0629516}";

	[System.Runtime.InteropServices.LibraryImport("shell32.dll", StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
	private static partial int SetCurrentProcessExplicitAppUserModelID(string id);

	public App()
	{
		// Before any window, and before registering for notifications, which use it.
		SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

		// One Totoro at a time: launched again, it shows the running one and exits, before loading plugins (which moves
		// their folders about, under the running one).
		var instance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("Totoro");
		if (!instance.IsCurrent)
		{
			var activation = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
			// Off the UI thread: waiting for it there could deadlock.
			Task.Run(() => instance.RedirectActivationToAsync(activation).AsTask()).Wait();
			Environment.Exit(0);
		}

		InitializeComponent();

		// A throw in an event handler (async void ones included) would otherwise close the app without a trace. The file
		// logger directly: the host isn't built yet while the setup wizard shows.
		UnhandledException += (_, e) =>
		{
			s_fileLogs.Error(e.Exception, "Unhandled exception");
			e.Handled = true;
		};
		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			s_fileLogs.Error(e.Exception, "Unobserved task exception");
			e.SetObserved();
		};
	}

	private Task<IServiceProvider>? _building;

	/// <summary>
	/// Builds the host (loading the plugins), once, off the UI thread so the setup wizard doesn't freeze. Only registering
	/// happens there: services are made when they're first asked for, on the UI thread.
	/// </summary>
	private Task<IServiceProvider> BuildHostAsync()
	{
		return _building ??= Task.Run(() =>
		{
			_host = Host.CreateDefaultBuilder()
				.ConfigureLogging(x => x.AddSerilog(s_fileLogs))
				.ConfigureServices((context, services) =>
				{
					AddModules(services);

					services.AddTotoroServices(() => _window);
				})
				.Build();
			Locator.SetServiceProvider(_host.Services);
			return _host.Services;
		});
	}

	protected override async void OnLaunched(LaunchActivatedEventArgs args)
	{
		SetupWindow? setup = null;
		if (_firstRun)
		{
			// Its plugin steps run before the host exists (a plugin manager of their own); its later steps build the host,
			// and show dialogs and sign-in over it, as the window until the main one opens.
			setup = new SetupWindow(new PluginManager(_pluginLoader, new Preferences()), BuildHostAsync);
			_window = setup;
			setup.Activate();
			await setup.Completion;
		}

		await BuildHostAsync();
		_window = new MainWindow()
		{
			ViewModel = _host.Services.GetRequiredService<MainWindowViewModel>(),
		};
		_host.Services.GetRequiredService<IAppearanceService>().Apply();
		// Follows playback from now on.
		_host.Services.GetRequiredService<DiscordPresence>();
		// Back in the watch-together room the user was in when the app last closed.
		_host.Services.GetRequiredService<SyncplayClient>().Rejoin();
		// Marks episodes finished in a browser watched, while the app's open.
		_host.Services.GetRequiredService<BrowserWatchTracker>();
		// Downloads the next episode of anime being watched, if that's turned on.
		_host.Services.GetRequiredService<AutoDownloader>();
		// Songs can be played, paused and skipped from Windows' media controls, with the app minimised.
		_host.Services.GetRequiredService<SongMediaControls>();
#if DEBUG
		// The agent tools at http://localhost:47700/tools, for coding agents (see the run-totoro skill).
		AgentToolServer.Start(_host.Services.GetRequiredService<IAgentTools>(), _host.Services.GetRequiredService<ILogger<App>>());
#endif
		// The Downloads item in the navigation shows how many downloads are going.
		var downloads = _host.Services.GetRequiredService<DownloadService>();
		var downloadsItem = _host.Services.GetServices<NavigationItemViewModel>().First(x => x.ViewModelType == typeof(DownloadsViewModel));
		downloads.PropertyChanged += (_, _) => downloadsItem.BadgeCount = downloads.ActiveCount;
		var window = _window;
		// The Plugins item shows how many plugins have an update that can be installed.
		var pluginManager = _host.Services.GetRequiredService<IPluginManager>();
		var pluginsItem = _host.Services.GetServices<NavigationItemViewModel>().First(x => x.ViewModelType == typeof(PluginsViewModel));
		WeakReferenceMessenger.Default.Register<App, PluginsChangedMessage>(this, (_, _) => window.DispatcherQueue.TryEnqueue(async () =>
			pluginsItem.BadgeCount = (await pluginManager.GetCatalogAsync()).Count(x => x.HasUpdate && x.IsCompatible)));
		var preferences = _host.Services.GetRequiredService<IPreferences>();
		var manager = WinUIEx.WindowManager.Get(window);
		// The pages reflow down to this, no further.
		manager.MinWidth = 900;
		manager.MinHeight = 600;
		var shown = false;
		var quitting = false;

		void Show()
		{
			// Through Windows: minimized while full screen (a player in it), the presenter can't restore it (WinUIEx's Restore
			// throws), and doesn't say it's minimized.
			var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
			if (IsIconic(hwnd))
			{
				ShowWindow(hwnd, 9 /* SW_RESTORE */);
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

		// A player opening in it shows it, from the tray or minimized too.
		if (window is MainWindow main)
		{
			main.BringToFront = Show;
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
					if (await dialog.ShowThemedAsync() != Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
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
			// A player playing in this window closes first, so what was watched is saved.
			(window as MainWindow)?.ClosePlayer();
			window.Close();
		}

		window.AppWindow.Closing += (sender, args) =>
		{
			if (quitting)
			{
				return;
			}

			args.Cancel = true;
			// A player playing in this window: the window's close button closes the player, back to the app.
			if ((window as MainWindow)?.ClosePlayer() == true)
			{
				return;
			}

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
		// Download notifications' clicks, while the app runs (one clicked after it quit just launches it).
		var notifier = _host.Services.GetRequiredService<DownloadNotifier>();
		var notifications = Microsoft.Windows.AppNotifications.AppNotificationManager.Default;
		notifications.NotificationInvoked += (_, e) => window.DispatcherQueue.TryEnqueue(() =>
		{
			if (notifier.Handle(e))
			{
				Show();
			}
		});
		var notificationsRegistered = false;
		try
		{
			using (var server = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"Software\Classes\CLSID\{ToastActivator}\LocalServer32"))
			{
				server.SetValue(null, $"\"{Environment.ProcessPath}\" ----AppNotificationActivated:");
			}

			using (var app = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{AppUserModelId}"))
			{
				app.SetValue("CustomActivator", ToastActivator);
			}

			notifications.Register();
			notificationsRegistered = true;
		}
		catch (Exception ex)
		{
			// E.g. the self-contained runtime missing Microsoft.WindowsAppRuntime.Insights.Resource.dll; the app still runs, without notifications.
			_host.Services.GetRequiredService<ILogger<App>>().LogWarning(ex, "Couldn't register for notifications");
		}
		// Another launch of Totoro, redirected here (see the constructor).
		Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().Activated += (_, _) => window.DispatcherQueue.TryEnqueue(() => Show());
		// Clicking the icon opens the window, so the menu only quits.
		manager.TrayIconContextMenu += (sender, e) =>
		{
			var quit = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Quit Totoro" };
			quit.Click += (_, _) => _ = QuitAsync();
			e.Flyout = new Microsoft.UI.Xaml.Controls.MenuFlyout { Items = { quit } };
		};
		// The app ends with its main window, the built-in player's window too.
		window.Closed += (_, _) =>
		{
			manager.IsVisibleInTray = false;
			if (notificationsRegistered)
			{
				notifications.Unregister();
			}
			s_fileLogs.Dispose();
			Exit();
		};
		// E.g. for the installer of a newer version to replace the app (see UpdateService).
		WeakReferenceMessenger.Default.Register<App, AppExitRequestedMessage>(this, (_, _) =>
		{
			quitting = true;
			(window as MainWindow)?.ClosePlayer();
			window.Close();
		});
		// Rewritten so an entry from before --startup existed gets it (and follows the exe if it moved).
		if (Helpers.WindowsStartup.IsEnabled)
		{
			Helpers.WindowsStartup.IsEnabled = true;
		}
		// Only when Windows starts it: started by the user, it always opens the window, after the splash.
		if (preferences.StartInTray && Helpers.WindowsStartup.Launched)
		{
			manager.IsVisibleInTray = true;
			// The tray keeps the app going without a window.
			setup?.Close();
			await RunStartupAsync(null);
		}
		else
		{
			var splash = new SplashWindow();
			splash.Activate();
			// After the splash opens: the app ends when its last window closes.
			setup?.Close();
			await RunStartupAsync(splash);
			// Shown before the splash closes, so the app always has a window open.
			Show();
			splash.Close();
		}
	}

	/// <summary>The work done before the window opens, while the splash shows. Each step reports what it's doing.</summary>
	private async Task RunStartupAsync(SplashWindow? splash)
	{
		void Status(string text) => splash?.Status = text;

		var services = _host.Services;
		UseMpvAsDefaultOnce(services);

		// The default player and torrent streamer are downloaded before the window opens, so the first video just plays.
		if (services.GetRequiredService<IFactory<IMediaPlayer>>().CreateDefault() is { } player &&
			player is Modules.MediaPlayers.Mpv.MpvMediaPlayer or Modules.MediaPlayers.Mpv.MpvWindowMediaPlayer && !player.IsAvailable())
		{
			var mpv = services.GetRequiredService<Modules.MediaPlayers.Mpv.MpvInstaller>();
			await DownloadToolAsync("mpv", mpv.Action, mpv.InstallAsync(), splash);
		}

		if (services.GetRequiredService<IFactory<ITorrentStreamer>>().CreateDefault() is Modules.TorrentStreamers.Rqbit.RqbitStreamer &&
			!Modules.TorrentStreamers.Rqbit.RqbitInstaller.IsInstalled)
		{
			var rqbit = services.GetRequiredService<Modules.TorrentStreamers.Rqbit.RqbitInstaller>();
			await DownloadToolAsync("rqbit", rqbit.Action, rqbit.InstallAsync(), splash);
		}

		// The Plugins page lists the catalog (and its item the updates) as soon as it opens.
		Status("Checking for plugin updates…");
		try
		{
			// Capped: a slow connection doesn't keep the window from opening (the page fetches it then).
			await services.GetRequiredService<IPluginManager>().GetCatalogAsync().WaitAsync(TimeSpan.FromSeconds(10));
		}
		catch (TimeoutException)
		{
		}

		// Not awaited: an update doesn't hold up the window, and nothing plays yet, so mpv isn't in use. Each catches its own errors.
		if (services.GetRequiredService<Modules.MediaPlayers.Mpv.MpvInstaller>() is { IsUpdateDue: true } mpvUpdate)
		{
			_ = mpvUpdate.InstallAsync();
		}

		if (services.GetRequiredService<Modules.TorrentStreamers.Rqbit.RqbitInstaller>() is { IsUpdateDue: true } rqbitUpdate)
		{
			_ = rqbitUpdate.InstallAsync();
		}
	}

	/// <summary>
	/// Waits for a tool's download while the splash shows how it's going. A failure only shows in its settings; playing
	/// offers it again. Capped generously: past that the window opens and the download goes on behind it.
	/// </summary>
	private static async Task DownloadToolAsync(string name, SettingsAction action, Task install, SplashWindow? splash)
	{
		splash?.Status = $"Downloading {name}…";
		void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (splash is not null && action.IsBusy && action.Description != "")
			{
				splash.Status = action.Description;
				splash.Progress = action.Progress;
			}
		}

		action.PropertyChanged += OnChanged;
		try
		{
			await install.WaitAsync(TimeSpan.FromMinutes(5));
		}
		catch (TimeoutException)
		{
		}
		finally
		{
			action.PropertyChanged -= OnChanged;
			splash?.Progress = null;
		}
	}

	// The libVLC player's, built in then (a plugin now).
	private static readonly Guid s_vlcModuleId = new("5e0a7c1f-3b9d-4e62-8a4f-2d7c9b1e6f30");

	/// <summary>
	/// Once: mpv in Totoro's window became the default player. Opening Settings saved the first player (VLC then) even when
	/// none was picked, so a stored VLC can't be told from no choice: both move to mpv. Other choices stay.
	/// </summary>
	private static void UseMpvAsDefaultOnce(IServiceProvider services)
	{
		const string Key = "MpvDefaultApplied";
		var preferences = services.GetRequiredService<IPreferences>();
		if (preferences.ReadKey(Key, false))
		{
			return;
		}

		if (preferences.SelectedMediaPlayer == Guid.Empty || preferences.SelectedMediaPlayer == s_vlcModuleId)
		{
			preferences.SelectedMediaPlayer = Modules.MediaPlayers.Mpv.MpvModule.Id;
			// They weren't playing in mpv, so its stored window choice was never theirs.
			var mpv = services.GetRequiredService<IModuleSettings<Modules.MediaPlayers.Mpv.Settings>>();
			mpv.Value.PlayInApp = true;
			mpv.Save();
		}

		preferences.SaveKey(Key, true);
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
			// The first of each kind is what a fresh install uses: AniList, and mpv in Totoro's window (downloaded as the app
			// first starts, see RunStartupAsync).
			new Modules.AnimeDatabase.Anilist.AnilistModule(),
			new Modules.AnimeDatabase.MyAnimeList.MyAnimeListModule(),
			new Modules.MediaPlayers.Mpv.MpvModule(),
			new Modules.MediaPlayers.MediaFoundation.MediaFoundationModule(),
			new Modules.TorrentStreamers.Rqbit.RqbitModule(),
			new Modules.MediaSegments.Aniskip.AniskipModule(),
			new Modules.ChatModels.Ollama.OllamaModule(),
			new Modules.ChatModels.OpenAICompatible.OpenAICompatibleModule()
		];
	}
}
