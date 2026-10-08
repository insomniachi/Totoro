using CommunityToolkit.WinUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.Navigation;

namespace Totoro.UI.Navigation;

internal class SplitViewPaneNavigator : FrameBasedNavigator
{
	private readonly SplitView _splitView;

	public SplitViewPaneNavigator(SplitView splitView, IViewRegistry viewRegistry, IServiceScopeFactory serviceScopeFactory, ILogger<SplitViewPaneNavigator> logger)
	: base(viewRegistry, serviceScopeFactory, logger)
	{
		_splitView = splitView;
	}

	protected override void OnNavigating()
	{
		_splitView.IsPaneOpen = true;
	}

	protected override Frame GetFrame() => _splitView.Pane.FindDescendant<Frame>()!;
}
