using Microsoft.UI.Xaml;
using Totoro.Core.Plugins;
using WinUIEx;

namespace Totoro.Setup;

/// <summary>
/// The first-run setup, shown on a fresh install before the main window. Its plugin steps run before the app's services
/// exist, so what they install loads when those are built: on reaching <see cref="AccountsStep"/>, which needs them.
/// </summary>
public sealed partial class SetupWindow : Window
{
	private readonly TaskCompletionSource _completion = new();
	private readonly Func<IServiceProvider> _buildServices;

	/// <param name="buildServices">Builds the app's services (loading the plugins), once.</param>
	public SetupWindow(IPluginManager plugins, Func<IServiceProvider> buildServices)
	{
		InitializeComponent();
		_buildServices = buildServices;
		AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Totoro.ico"));
		// Only the wizard, as the splash shows: no title bar, sized to the wizard (its steps scroll their own lists).
		if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
		{
			presenter.SetBorderAndTitleBar(true, false);
			presenter.IsResizable = false;
			presenter.IsMaximizable = false;
		}

		this.CenterOnScreen(680, 660);

		_ = DatabasesStep.LoadAsync(plugins, "AnimeDatabase", builtIn: ["AniList", "MyAnimeList"]);
		_ = ProvidersStep.LoadAsync(plugins, "AnimeProvider");
		Wizard.StepChanged += OnStepChanged;
		Wizard.Completed += (_, _) => _completion.TrySetResult();

		// Closing it skips the rest; the app closes it once the splash is open (the app ends with its last window).
		AppWindow.Closing += (_, args) =>
		{
			args.Cancel = !_completion.Task.IsCompleted;
			_completion.TrySetResult();
		};
	}

	/// <summary>Done, skipped or closed, with any download finished: the app goes on to open.</summary>
	public Task Completion => CompleteAsync();

	private async Task CompleteAsync()
	{
		await _completion.Task;
		await Installing();
	}

	// A plugin half copied wouldn't load. A failed download has said so on its row.
	private Task Installing() => Task.WhenAll(DatabasesStep.Installing, ProvidersStep.Installing).ContinueWith(_ => { }, TaskScheduler.Default);

	private async void OnStepChanged(object? sender, int index)
	{
		if (index < Wizard.Steps.IndexOf(AccountsItem) || AccountsStep.IsReady)
		{
			return;
		}

		await Installing();
		// Installed from here on, a plugin would need a restart: the Plugins page does that.
		DatabasesStep.Lock();
		ProvidersStep.Lock();
		AccountsStep.Load(_buildServices());
	}
}
