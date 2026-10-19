using Microsoft.UI.Xaml.Controls;
using Totoro.UI.Navigation;
using Totoro.ViewModels;

namespace Totoro.Views.Settings;

/// <summary>A settings category's title, under Settings.</summary>
public sealed partial class SettingsHeader : UserControl
{
	private string _title = "";

	public SettingsHeader()
	{
		InitializeComponent();
	}

	/// <summary>The category, e.g. "Playback".</summary>
	public string Title
	{
		get => _title;
		set
		{
			_title = value;
			Breadcrumb.ItemsSource = new[] { "Settings", value };
		}
	}

	private void OnItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
	{
		// The categories page is the first one registered for SettingsViewModel (see App).
		if (args.Index == 0 && Navigators.TryGetNavigator("main", out var main))
		{
			_ = main.NavigateToViewModelAsync(typeof(SettingsViewModel));
		}
	}
}
