using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Modules;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Settings;
using Totoro.ViewModels;

namespace Totoro.Setup;

/// <summary>The setup step to pick the anime database and sign in: the built-in ones, and those the plugin steps added.</summary>
public sealed partial class AccountsStep : UserControl
{
	private IPreferences? _preferences;

	public AccountsStep()
	{
		InitializeComponent();
	}

	/// <summary>Whether it's been given the app's services.</summary>
	public bool IsReady => _preferences is not null;

	public void Load(IServiceProvider services)
	{
		_preferences = services.GetRequiredService<IPreferences>();
		// As the title bar's account flyout lists them (MainWindowViewModel).
		var modules = services.GetServices<Descriptor>().ToList();
		List<AnimeDatabaseOption> databases = [.. services.GetRequiredService<IFactory<IAnimeMetadataProvider>>().CreateAll()
			.Select(x => new AnimeDatabaseOption(x.Id, x.Name, modules.FirstOrDefault(m => m.Id == x.Id)?.Image ?? ""))];
		DatabaseBox.ItemsSource = databases;
		// Unset means the first, as in the database factory.
		DatabaseBox.SelectedItem = databases.FirstOrDefault(x => x.Id == _preferences.SelectedAnimeDatabase) ?? databases.FirstOrDefault();
		AccountsList.ItemsSource = services.GetServices<IAccount>().Select(x => new AccountViewModel(x)).ToList();

		LoadingRing.IsActive = false;
		LoadingRing.Visibility = Visibility.Collapsed;
		Body.Visibility = Visibility.Visible;
	}

	private void OnDatabaseChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_preferences is not null && DatabaseBox.SelectedItem is AnimeDatabaseOption option && option.Id != _preferences.SelectedAnimeDatabase)
		{
			_preferences.SelectedAnimeDatabase = option.Id;
		}
	}
}
