using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>
/// Playgrounds for reusable pieces of the app, one per tab: each is a page of its own (see ViewModels/Test), shown in
/// the page's frame by the <see cref="NavigatorKey"/> navigator.
/// </summary>
public partial class TestViewModel : ObservableObject
{
	/// <summary>The navigator of the page's frame, which shows the selected playground.</summary>
	public const string NavigatorKey = "test";

	// The playgrounds' navigation keys (their pages are registered with AddKeyedViewMap); the tabs carry them as Tag.
	public const string SettingsEditorKey = "Test.SettingsEditor";
	public const string AgentToolsKey = "Test.AgentTools";
	public const string BuiltInPlayerKey = "Test.BuiltInPlayer";
	public const string PluginsKey = "Test.Plugins";

	public TestViewModel()
	{
		// The frame's navigator registers once the page has loaded, after this; show the first playground then.
		Navigators.NavigatorAdded += OnNavigatorAdded;
	}

	/// <summary>The key of the playground being shown.</summary>
	[ObservableProperty]
	public partial string SelectedPlayground { get; set; } = SettingsEditorKey;

	partial void OnSelectedPlaygroundChanged(string value) => _ = ShowAsync();

	private void OnNavigatorAdded(object? sender, NavigatorAddedEventArgs e)
	{
		if (e.Key == NavigatorKey)
		{
			Navigators.NavigatorAdded -= OnNavigatorAdded;
			_ = ShowAsync();
		}
	}

	private Task ShowAsync()
	{
		return Navigators.TryGetNavigator(NavigatorKey, out var navigator) ? navigator.NavigateToKeyAsync(SelectedPlayground) : Task.CompletedTask;
	}
}
