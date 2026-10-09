using CommunityToolkit.WinUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Navigation;

namespace Totoro.UI.Navigation;

internal class SplitViewPaneNavigator : FrameBasedNavigator
{
	private readonly SplitView _splitView;
	private readonly Frame _frame;

	public SplitViewPaneNavigator(SplitView splitView, IViewRegistry viewRegistry, IServiceScopeFactory serviceScopeFactory, ILogger<SplitViewPaneNavigator> logger)
	: base(viewRegistry, serviceScopeFactory, logger)
	{
		_splitView = splitView;
		_frame = FindFrame(splitView) ?? throw new InvalidOperationException("SplitView.Pane must contain a Frame");

		// Same wiring as the other navigators, so pane pages get a view model (in their own scope)
		_frame.Navigated += OnFrameNavigated;
		_frame.Navigating += OnFrameNavigating;

		_splitView.PaneClosed += OnPaneClosed;
	}

	protected override void OnNavigating()
	{
		_splitView.IsPaneOpen = true;
	}

	protected override Frame GetFrame() => _frame;

	// Closing the pane (light dismiss or IsPaneOpen = false) ends the pane's session:
	// the page's view model gets OnNavigatedFrom, its scope is disposed and the pane history is cleared,
	// so the next open starts fresh instead of keeping a hidden page alive.
	private async void OnPaneClosed(SplitView sender, object args)
	{
		await ClearAsync();
	}

	// Look at the pane's direct children first: they exist before the pane is
	// ever opened, while a visual-tree search may not find anything until then.
	private static Frame? FindFrame(SplitView splitView) => splitView.Pane switch
	{
		Frame frame => frame,
		Panel panel => panel.Children.OfType<Frame>().FirstOrDefault() ?? panel.FindDescendant<Frame>(),
		DependencyObject other => other.FindDescendant<Frame>(),
		_ => null
	};
}
