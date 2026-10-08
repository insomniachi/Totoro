using CommunityToolkit.WinUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.Navigation;

namespace Totoro.UI.Navigation;

internal class NavigationViewNavigator : FrameBasedNavigator
{
	private readonly NavigationView _navigationView;

	public NavigationViewNavigator(NavigationView navigationView, IViewRegistry viewRegistry, IServiceScopeFactory serviceScopeFactory, ILogger<NavigationViewNavigator> logger)
		:base(viewRegistry, serviceScopeFactory, logger)
	{
		_navigationView = navigationView;

		_navigationView.SelectionChanged += async (s, e) =>
		{
			if (e.SelectedItem is NavigationItemViewModel vm)
			{
				await NavigateToViewModel(vm.ViewModelType);
			}
		};
		var frame = GetFrame();
		frame.Navigated += OnFrameNavigated;
		frame.Navigating += OnFrameNavigating;
	}

	protected override Frame GetFrame()
	{
		return _navigationView.FindDescendant<Frame>(x => x.Name == "Main") ?? throw new Exception("Frame not found");
	}
}
