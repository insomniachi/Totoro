using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Module;
using Totoro.Core.Module.Anime;
using Totoro.Core.Navigation;
using Totoro.Core.Settings;

namespace Totoro.ViewModels;

public sealed record AnimeDatabaseOption(Guid Id, string Name);

public partial class SettingsViewModel : ObservableObject
{
	private readonly IPreferences _preferences;

	public SettingsViewModel(IEnumerable<NavigationItemViewModel> navigationItems, IPreferences preferences, IFactory<IAnimeTracker, Guid> trackers)
	{
		_preferences = preferences;

		// Footer items (like Settings itself) aren't offered as a startup page.
		StartupPages = [.. navigationItems.Where(x => !x.IsFooter)];
		StartupPage = preferences.FindStartupItem(StartupPages);

		// Trackers and metadata providers come in pairs per module, so listing the trackers lists the databases.
		AnimeDatabases = [.. trackers.CreateAll().Select(x => new AnimeDatabaseOption(x.Id, x.Name))];
		AnimeDatabase = AnimeDatabases.FirstOrDefault(x => x.Id == preferences.SelectedAnimeDatabase) ?? AnimeDatabases.FirstOrDefault();
	}

	public IReadOnlyList<AnimeDatabaseOption> AnimeDatabases { get; }

	/// <summary>Selects the module whose tracker and metadata provider the app uses.</summary>
	[ObservableProperty]
	public partial AnimeDatabaseOption? AnimeDatabase { get; set; }

	partial void OnAnimeDatabaseChanged(AnimeDatabaseOption? value)
	{
		if (value is not null && value.Id != _preferences.SelectedAnimeDatabase)
		{
			_preferences.SelectedAnimeDatabase = value.Id;
		}
	}

	public IReadOnlyList<NavigationItemViewModel> StartupPages { get; }

	[ObservableProperty]
	public partial NavigationItemViewModel? StartupPage { get; set; }

	partial void OnStartupPageChanged(NavigationItemViewModel? value)
	{
		if (value is not null && value.Label != _preferences.StartupPage)
		{
			_preferences.StartupPage = value.Label;
		}
	}
}
