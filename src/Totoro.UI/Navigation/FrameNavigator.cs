using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.Navigation;

namespace Totoro.UI.Navigation;

internal class FrameNavigator : FrameBasedNavigator
{
	private readonly Frame _frame;

	public FrameNavigator(Frame frame, IViewRegistry viewRegistry, IServiceScopeFactory serviceScopeFactory, ILogger<FrameNavigator> logger)
		: base(viewRegistry, serviceScopeFactory, logger)
	{
		_frame = frame;
		_frame.Navigated += OnFrameNavigated;
		_frame.Navigating += OnFrameNavigating;
	}

	protected override Frame GetFrame() => _frame;
}
