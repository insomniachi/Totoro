using CommunityToolkit.WinUI.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Totoro.Core;
using Totoro.Core.Navigation;

namespace Totoro.UI.Navigation;

public partial class RegisterForNavigationBehavior : BehaviorBase<Control>
{
	public string NavigatorKey { get; set; } = "main";

	protected override void OnAssociatedObjectLoaded()
	{
		INavigator? navigator = null;
		var viewRegistry = Locator.GetRequiredService<IViewRegistry>();
		var scopeFactory = Locator.GetRequiredService<IServiceScopeFactory>();

		if (AssociatedObject is NavigationView nv)
		{
			navigator = new NavigationViewNavigator(nv, viewRegistry, scopeFactory, Locator.GetRequiredService<ILogger<NavigationViewNavigator>>());
		}
		else if(AssociatedObject is Frame frame)
		{
			navigator = new FrameNavigator(frame, viewRegistry, scopeFactory, Locator.GetRequiredService<ILogger<FrameNavigator>>());
		}
		else if (AssociatedObject is SplitView sv)
		{
			navigator = new SplitViewPaneNavigator(sv, viewRegistry, scopeFactory, Locator.GetRequiredService<ILogger<SplitViewPaneNavigator>>());
		}

		if (navigator is not null)
		{
			Navigators.AddNavigator(NavigatorKey, navigator);
		}
	}
}
