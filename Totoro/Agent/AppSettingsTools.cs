using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Totoro.Core.Agent;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Modules.Media;
using Totoro.Core.Navigation;
using Totoro.Core.Settings;

namespace Totoro.Agent;

[Description("The app's settings.")]
public sealed record AppSettings(
	AppTheme Theme,
	AppBackdrop Backdrop,
	[property: Description("Where the user's list and anime information come from; one of animeDatabases.")]
	string AnimeDatabase,
	IReadOnlyList<string> AnimeDatabases,
	[property: Description("Where episodes play; one of mediaPlayers. The built-in player offers skipping and the next episode over the video, and can be controlled with the playback tools.")]
	string MediaPlayer,
	IReadOnlyList<string> MediaPlayers,
	[property: Description("What happens when an opening starts: Off (nothing), Ask (offer to skip) or Automatic (skip it).")]
	SkipMode OpeningSkipMode,
	SkipMode EndingSkipMode,
	[property: Description("Whether the next episode plays by itself when one is finished.")]
	bool PlayNextAutomatically,
	[property: Description("The page the app opens on; one of startupPages.")]
	string StartupPage,
	IReadOnlyList<string> StartupPages);

[Description("A module (an anime database, provider, media player...) and its settings.")]
public sealed record ModuleSettingsInfo(string Module, IReadOnlyList<ModuleSettingInfo> Settings);

[Description("A setting of a module.")]
public sealed record ModuleSettingInfo(
	[property: Description("What update_module_setting takes.")]
	string Name,
	string DisplayName,
	string? Description,
	string Value,
	[property: Description("The values it can have, if it's a choice.")]
	IReadOnlyList<string>? AllowedValues);

[Description("An online account (e.g. AniList) the user can sign in to.")]
public sealed record AccountInfo(string Service, bool IsSignedIn, string? UserName);

/// <summary>Agent tools for the app's settings, its modules' settings and the user's accounts.</summary>
public sealed class AppSettingsTools(
	IPreferences preferences,
	IAppearanceService appearance,
	IFactory<IAnimeTracker, Guid> trackers,
	IFactory<IMediaPlayer, Guid> players,
	IEnumerable<Descriptor> modules,
	IEnumerable<IModuleSettings> moduleSettings,
	IEnumerable<NavigationItemViewModel> pages,
	IEnumerable<IAccount> accounts)
{
	[AgentTool("get_app_settings")]
	[Description("The app's settings: theme, anime database, media player, skipping, playing the next episode, startup page.")]
	public AppSettings GetAppSettings()
	{
		var databases = trackers.CreateAll().ToList();
		var selected = databases.FirstOrDefault(x => x.Id == preferences.SelectedAnimeDatabase) ?? databases.FirstOrDefault();
		var startupPages = pages.Where(x => !x.IsFooter).ToList();
		var mediaPlayers = GetMediaPlayers();
		var mediaPlayer = mediaPlayers.FirstOrDefault(x => x.Id == preferences.SelectedMediaPlayer) ?? mediaPlayers.FirstOrDefault();
		return new AppSettings(
			appearance.Theme,
			appearance.Backdrop,
			selected?.Name ?? "",
			[.. databases.Select(x => x.Name)],
			mediaPlayer?.Name ?? "",
			[.. mediaPlayers.Select(x => x.Name)],
			preferences.OpeningSkipMode,
			preferences.EndingSkipMode,
			preferences.PlayNextAutomatically,
			preferences.FindStartupItem(startupPages)?.Label ?? "",
			[.. startupPages.Select(x => x.Label)]);
	}

	[AgentTool("update_app_settings")]
	[Description("Changes the app's settings. Only the values given change; theme and backdrop apply straight away. Returns the settings as they are now.")]
	public AppSettings UpdateAppSettings(
		AppTheme? theme = null,
		AppBackdrop? backdrop = null,
		[Description("One of animeDatabases from get_app_settings.")] string? animeDatabase = null,
		[Description("One of mediaPlayers from get_app_settings.")] string? mediaPlayer = null,
		SkipMode? openingSkipMode = null,
		SkipMode? endingSkipMode = null,
		bool? playNextAutomatically = null,
		[Description("One of startupPages from get_app_settings.")] string? startupPage = null)
	{
		if (animeDatabase is not null)
		{
			var databases = trackers.CreateAll().ToList();
			preferences.SelectedAnimeDatabase = databases.FirstOrDefault(x => x.Name.Equals(animeDatabase, StringComparison.OrdinalIgnoreCase))?.Id
				?? throw new AgentToolException($"There's no anime database called {animeDatabase}; they're {string.Join(", ", databases.Select(x => x.Name))}.");
		}

		if (mediaPlayer is not null)
		{
			var mediaPlayers = GetMediaPlayers();
			preferences.SelectedMediaPlayer = mediaPlayers.FirstOrDefault(x => x.Name.Equals(mediaPlayer, StringComparison.OrdinalIgnoreCase))?.Id
				?? throw new AgentToolException($"There's no media player called {mediaPlayer}; they're {string.Join(", ", mediaPlayers.Select(x => x.Name))}.");
		}

		if (startupPage is not null)
		{
			var page = pages.Where(x => !x.IsFooter).FirstOrDefault(x => x.Label.Equals(startupPage, StringComparison.OrdinalIgnoreCase))
				?? throw new AgentToolException($"There's no page called {startupPage}.");
			preferences.StartupPage = page.Label;
		}

		if (theme is { } newTheme)
		{
			appearance.Theme = newTheme;
		}

		if (backdrop is { } newBackdrop)
		{
			appearance.Backdrop = newBackdrop;
		}

		if (openingSkipMode is { } opening)
		{
			preferences.OpeningSkipMode = opening;
		}

		if (endingSkipMode is { } ending)
		{
			preferences.EndingSkipMode = ending;
		}

		if (playNextAutomatically is { } playNext)
		{
			preferences.PlayNextAutomatically = playNext;
		}

		return GetAppSettings();
	}

	[AgentTool("get_module_settings")]
	[Description("The settings of the app's modules: anime databases, anime providers, media players, torrent streaming...")]
	public IReadOnlyList<ModuleSettingsInfo> GetModuleSettings([Description("Only this module's.")] string? module = null)
	{
		return
		[
			.. moduleSettings.Where(x => module is null || x.Descriptor.Name.Equals(module, StringComparison.OrdinalIgnoreCase))
							 .Select(x => new ModuleSettingsInfo(x.Descriptor.Name, [.. x.Config.ToDataContainer().Select(Describe)]))
							 .Where(x => x.Settings.Count > 0)
		];
	}

	[AgentTool("update_module_setting")]
	[Description("Changes a setting of a module (from get_module_settings).")]
	public ModuleSettingsInfo UpdateModuleSetting(
		string module,
		[Description("The setting's name.")] string setting,
		[Description("Its new value: true or false, a number, one of its allowedValues, or text.")] string value)
	{
		var settings = moduleSettings.FirstOrDefault(x => x.Descriptor.Name.Equals(module, StringComparison.OrdinalIgnoreCase))
			?? throw new AgentToolException($"There's no module called {module}.");
		var property = settings.Config.GetType().GetProperty(setting, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
			?? settings.Config.GetType().GetProperties().FirstOrDefault(x => settings.Config.ToDataContainer().GetProperty(x.Name)?.DisplayName?
																						.Equals(setting, StringComparison.OrdinalIgnoreCase) == true)
			?? throw new AgentToolException($"{settings.Descriptor.Name} has no setting called {setting}.");

		property.SetValue(settings.Config, ParseValue(value, property.PropertyType));
		settings.Save();
		return new ModuleSettingsInfo(settings.Descriptor.Name, [.. settings.Config.ToDataContainer().Select(Describe)]);
	}

	[AgentTool("get_accounts")]
	[Description("The online accounts the user can sign in to, and whether they are.")]
	public async Task<IReadOnlyList<AccountInfo>> GetAccountsAsync()
	{
		List<AccountInfo> result = [];
		foreach (var account in accounts)
		{
			var user = account.IsSignedIn ? await account.GetUserAsync() : null;
			result.Add(new AccountInfo(account.Name, account.IsSignedIn, user?.Name));
		}

		return result;
	}

	[AgentTool("sign_in")]
	[Description("Starts signing in to an account; the user finishes it in the window that opens.")]
	public async Task<string> SignInAsync(string service)
	{
		var account = FindAccount(service);
		return await account.SignInAsync() ? $"Signed in to {account.Name}." : $"Didn't sign in to {account.Name}.";
	}

	[AgentTool("sign_out", RequiresApproval = true)]
	[Description("Signs out of an account.")]
	public string SignOut(string service)
	{
		var account = FindAccount(service);
		account.SignOut();
		return $"Signed out of {account.Name}.";
	}

	/// <summary>The media players, named as their modules are.</summary>
	private List<NamedPlayer> GetMediaPlayers()
	{
		return [.. players.CreateAll().Select(x => new NamedPlayer(x.Id, modules.FirstOrDefault(m => m.Id == x.Id)?.Name ?? x.Name))];
	}

	private sealed record NamedPlayer(Guid Id, string Name);

	private IAccount FindAccount(string service)
	{
		return accounts.FirstOrDefault(x => x.Name.Equals(service, StringComparison.OrdinalIgnoreCase))
			?? throw new AgentToolException($"There's no account for {service}; they're {string.Join(", ", accounts.Select(x => x.Name))}.");
	}

	private static ModuleSettingInfo Describe(DataContainerProperty property)
	{
		return new ModuleSettingInfo(
			property.Name,
			property.DisplayName ?? property.Name,
			property.Description,
			Convert.ToString(property.Value, CultureInfo.InvariantCulture) ?? "",
			property.AllowedValues?.Any() == true ? [.. property.AllowedValues.Select(x => x.ToString() ?? "")] : null);
	}

	private static object ParseValue(string value, Type type)
	{
		try
		{
			if (type.IsEnum)
			{
				return Enum.Parse(type, value, ignoreCase: true);
			}

			return type == typeof(string) ? value : System.Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
		}
		catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidCastException or OverflowException)
		{
			var expected = type.IsEnum ? string.Join(", ", Enum.GetNames(type)) : type == typeof(bool) ? "true or false" : "a number";
			throw new AgentToolException($"\"{value}\" isn't a valid value; it takes {expected}.");
		}
	}
}
