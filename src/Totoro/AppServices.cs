using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Serilog;
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
using Totoro.Core.Modules.Manga;
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
using Totoro.ViewModels.Manga;
using Totoro.Views;
using Totoro.Views.Manga;
using Totoro.Views.Relations;
using Totoro.Views.Settings;

namespace Totoro;

/// <summary>The app's services, pages and dialogs, registered with the host (after the plugins' modules).</summary>
internal static class AppServices
{
	/// <param name="window">The main window, once it's open: what dialogs, sign-in and web pages show over.</param>
	public static IServiceCollection AddTotoroServices(this IServiceCollection services, Func<Window?> window)
	{
		// A module provides its tracker and metadata provider as a pair, so one setting picks both.
		services.RegisterFactory<IAnimeTracker>(AnimeDatabasePreferences.Key);
		services.RegisterFactory<IAnimeMetadataProvider>(AnimeDatabasePreferences.Key);
		services.RegisterFactory<IAnimeProvider>(PlaybackPreferences.AnimeProviderKey);
		services.RegisterFactory<IMediaPlayer>(PlaybackPreferences.MediaPlayerKey);
		services.RegisterFactory<ITorrentStreamer>(PlaybackPreferences.TorrentStreamerKey);
		services.RegisterFactory<IChatModel>("SelectedChatModel");
		// Manga: a database (chosen apart from the anime one) and a source to read from.
		services.RegisterFactory<IMangaTracker>(MangaPreferences.DatabaseKey);
		services.RegisterFactory<IMangaMetadataProvider>(MangaPreferences.DatabaseKey);
		services.RegisterFactory<IMangaProvider>(MangaPreferences.ProviderKey);

		// Watching: the watch service drives a playback from a card's Play.
		services.AddSingleton<WatchService>();
		// What the provider has of an anime's new episodes, for the cards' badges.
		services.AddSingleton<IProviderEpisodes, ProviderEpisodes>();
		services.AddSingleton<DownloadService>();
		services.AddSingleton<DiscordPresence>();
		services.AddSingleton<SyncplayClient>();
		services.AddSingleton<BrowserWatchTracker>();
		services.AddSingleton<AutoDownloader>();
		services.AddSingleton<DownloadNotifier>();
		// Songs' audio, played in the Music tab.
		services.AddSingleton<ISongPlayer, SongAudioPlayer>();
		services.AddDialog<ShowPickerDialog, ShowPickerDialogViewModel>();

		services.AddCoreServices();
		services.AddSingleton<MainWindowViewModel>();
		services.AddSingleton<IAppearanceService>(sp => new AppearanceService(sp.GetRequiredService<IPreferences>(), window));
		services.AddSingleton<IDialogService>(sp => new DialogService(
			sp.GetRequiredService<IViewRegistry>(),
			sp.GetRequiredService<IServiceScopeFactory>(),
			() => window()?.Content?.XamlRoot));
		services.AddSingleton<IWebAuthenticator>(_ => new WebAuthenticator(() => window()?.Content?.XamlRoot));
		// Pages from sites that only serve browsers (e.g. AnimePahe, behind Cloudflare).
		services.AddSingleton<IWebPageLoader>(sp => new WebPageLoader(
			() => window() is not { } open ? throw new InvalidOperationException("The app's window isn't open yet.") : WinRT.Interop.WindowNative.GetWindowHandle(open),
			() => window()?.Content?.XamlRoot,
			sp.GetRequiredService<IMainThread>()));

		// Everything the app does, as tools an AI agent can call (see Agent/). A new feature adds its tools here too.
		services.AddSingleton<IMainThread>(_ => new MainThread(() => window()?.DispatcherQueue));
		services.AddTransient<AnimeLookup>();
		services.AddAgentTools<AnimeTools>()
				.AddAgentTools<PlaybackTools>()
				.AddAgentTools<WatchTogetherTools>()
				.AddAgentTools<AnimeSettingsTools>()
				.AddAgentTools<AppSettingsTools>()
				.AddAgentTools<NavigationTools>()
				.AddAgentTools<RelationsTools>()
				.AddAgentTools<UpdateTools>()
				.AddAgentTools<AnimePluginTools>()
				.AddAgentTools<PluginTools>()
				.AddAgentTools<ReleaseScheduleTools>()
				.AddAgentTools<MangaTools>();
		// The assistant: a chat that calls those tools (see ChatService).
		services.AddSingleton<ChatViewModel>();

		services.AddDialog<TrackingDialog, TrackingDialogViewModel>();
		services.AddDialog<DownloadEpisodesDialog, DownloadEpisodesDialogViewModel>();
		services.AddDialog<DownloadInfoDialog, DownloadInfoDialogViewModel>();
		services.AddDialog<AnimeSettingsDialog, AnimeSettingsDialogViewModel>();
		// Episode numbering: anime-relations (downloaded when first needed) and the user's own rules.
		services.AddSingleton<IAnimeRelations, AnimeRelations>();
		services.AddViewMap<AnimeRelationsPage, AnimeRelationsViewModel>();
		services.AddViewMap<MissedRelationsPage, MissedRelationsViewModel>();
		services.AddDialog<RelationRuleDialog, RelationRuleDialogViewModel>();
		services.AddDialog<DataContainerDialog, DataContainerDialogViewModel>();
		services.AddSingleton<AnimeCommands>();
		services.AddDataViewMap<AnimeDetailsPage, AnimeDetailsViewModel, AnimeModel>();
		services.AddSingleton<MangaCommands>();
		services.AddDataViewMap<MangaDetailsPage, MangaDetailsViewModel, MangaModel>();
		services.AddDialog<MangaTrackingDialog, MangaTrackingDialogViewModel>();
		services.AddDataViewMap<ReaderPage, ReaderViewModel, ReaderRequest>();
		// First, so it's where the app starts until the user picks another startup page (see StartupPagePreferences).
		services.AddMainNavigationItem<DiscoverPage, DiscoverViewModel>("Discover", x =>
		{
			x.Mode = MediaMode.Anime;
			x.AnimatedIcon = () => Icons.DiscoverAnimated;
		});
		services.AddMainNavigationItem<UserListPage, UserListViewModel>("Library", x =>
		{
			x.Mode = MediaMode.Anime;
			x.AnimatedIcon = () => Icons.LibraryAnimated;
		});
		services.AddMainNavigationItem<AdvancedSearchPage, AdvancedSearchViewModel>("Search", x =>
		{
			x.Mode = MediaMode.Anime;
			x.AnimatedIcon = () => Icons.SearchAnimated;
		});
		// Manga mode's pages: the same names as anime's where they do the same, so switching mode keeps the user's place.
		services.AddMainNavigationItem<MangaDiscoverPage, MangaDiscoverViewModel>("Discover", x =>
		{
			x.Mode = MediaMode.Manga;
			x.AnimatedIcon = () => Icons.DiscoverAnimated;
		}, key: "MangaDiscover");
		services.AddMainNavigationItem<MangaLibraryPage, MangaLibraryViewModel>("Library", x =>
		{
			x.Mode = MediaMode.Manga;
			x.AnimatedIcon = () => Icons.LibraryAnimated;
		}, key: "MangaLibrary");
		services.AddMainNavigationItem<MangaSearchPage, MangaSearchViewModel>("Search", x =>
		{
			x.Mode = MediaMode.Manga;
			x.AnimatedIcon = () => Icons.SearchAnimated;
		}, key: "MangaSearch");
		services.AddMainNavigationItem<MangaReadPage, MangaReadViewModel>("Read", x =>
		{
			x.Mode = MediaMode.Manga;
			x.AnimatedIcon = () => Icons.ReadAnimated;
		});
		services.AddMainNavigationItem<ReleaseSchedulePage, ReleaseScheduleViewModel>("Schedule", x =>
		{
			x.Mode = MediaMode.Anime;
			x.AnimatedIcon = () => Icons.ScheduleAnimated;
		});
		services.AddMainNavigationItem<ProviderWatchPage, ProviderWatchViewModel>("Watch", x =>
		{
			x.Mode = MediaMode.Anime;
			x.AnimatedIcon = () => Icons.PlayAnimated;
		});
		services.AddMainNavigationItem<DownloadsPage, DownloadsViewModel>("Downloads", x =>
		{
			x.Mode = MediaMode.Anime;
			x.AnimatedIcon = () => Icons.DownloadAnimated;
		});
		services.AddMainNavigationItem<PluginsPage, PluginsViewModel>("Plugins", x =>
		{
			x.AnimatedIcon = () => Icons.PluginsAnimated;
		});
#if DEBUG
		// A developer's playground: not for users.
		services.AddMainNavigationItem<AgentToolsPage, AgentToolsViewModel>("Agent tools", x =>
		{
			x.AnimatedIcon = () => Icons.AgentToolsAnimated;
		});
#endif
		services.AddViewMap<PluginTesterPage, PluginTesterViewModel>();
		services.AddMainNavigationItem<SettingsPage, SettingsViewModel>("Settings", x =>
		{
			x.AnimatedIcon = () => Icons.SettingsAnimated;
			x.IsFooter = true;
		});
		// Settings' categories, after it: the navigator finds the first page registered for SettingsViewModel.
		services.AddKeyedViewMap<GeneralSettingsPage, SettingsViewModel>(SettingsViewModel.GeneralKey);
		services.AddKeyedViewMap<AccountsSettingsPage, SettingsViewModel>(SettingsViewModel.AccountsKey);
		services.AddKeyedViewMap<PlaybackSettingsPage, SettingsViewModel>(SettingsViewModel.PlaybackKey);
		services.AddKeyedViewMap<DownloadsSettingsPage, SettingsViewModel>(SettingsViewModel.DownloadsKey);
		services.AddKeyedViewMap<AboutSettingsPage, SettingsViewModel>(SettingsViewModel.AboutKey);

		return services;
	}
}
