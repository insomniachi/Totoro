using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Playback;
using Totoro.UI.Navigation;
using Totoro.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Totoro
{
	/// <summary>
	/// An empty window that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
			ExtendsContentIntoTitleBar = true;
			SetTitleBar(AppTitleBar);
			AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;
			// The back button is hidden (not just disabled) until there is somewhere to go back to.
			AppTitleBar.BackRequested += async (_, _) =>
			{
				if (Navigators.TryGetNavigator(MainNavigatorKey, out var navigator))
				{
					await navigator.GoBack();
				}
			};

			// The "main" navigator is created when the NavigationView loads, after this constructor runs.
			Navigators.NavigatorAdded += OnNavigatorAdded;
		}

		private const string MainNavigatorKey = "main";

		private void OnNavigatorAdded(object? sender, NavigatorAddedEventArgs e)
		{
			if (e.Key != MainNavigatorKey)
			{
				return;
			}

			Navigators.NavigatorAdded -= OnNavigatorAdded;
			var navigator = e.Navigator;
			navigator.Navigated += (_, _) => AppTitleBar.IsBackButtonVisible = navigator.CanGoBack;

			// Selecting the item makes the navigator navigate to it, and keeps the menu highlight in sync.
			NavView.SelectedItem = ViewModel?.StartupItem;
		}

		public MainWindowViewModel? ViewModel { get; set; }

		// For the status bar's x:Bind function bindings.

		public static InfoBarSeverity ToSeverity(PlaybackStatusKind kind) => kind switch
		{
			PlaybackStatusKind.Success => InfoBarSeverity.Success,
			PlaybackStatusKind.Error => InfoBarSeverity.Error,
			_ => InfoBarSeverity.Informational,
		};

		/// <summary>The progress bar and Cancel, while getting an episode ready.</summary>
		public static Visibility VisibleWhileWorking(PlaybackStatusKind kind) => kind == PlaybackStatusKind.Progress ? Visibility.Visible : Visibility.Collapsed;
	}
}
