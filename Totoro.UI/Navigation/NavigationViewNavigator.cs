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
	private bool _isSyncingSelection;

	public NavigationViewNavigator(NavigationView navigationView, IViewRegistry viewRegistry, IServiceScopeFactory serviceScopeFactory, ILogger<NavigationViewNavigator> logger)
		:base(viewRegistry, serviceScopeFactory, logger)
	{
		_navigationView = navigationView;

		_navigationView.SelectionChanged += async (s, e) =>
		{
			if (_isSyncingSelection)
			{
				return;
			}

			// Already showing this page (e.g. selection synced after GoBack), nothing to do.
			if (e.SelectedItem is NavigationItemViewModel vm && vm.ViewModelType != GetCurrentViewModelType())
			{
				await NavigateToViewModelAsync(vm.ViewModelType);
			}
		};
		var frame = GetFrame();
		frame.Navigated += OnFrameNavigated;
		// Navigation not started from the menu (e.g. by an agent tool) still highlights its page.
		frame.Navigated += (_, _) => SyncSelection();
		frame.Navigating += OnFrameNavigating;
	}

	protected override void OnWentBack() => SyncSelection();

	/// <summary>Highlights the menu item of the page being shown, without navigating again.</summary>
	private void SyncSelection()
	{
		if (GetCurrentViewModelType() is not { } vmType)
		{
			return;
		}

		var item = EnumerateItems(_navigationView.MenuItemsSource)
			.Concat(EnumerateItems(_navigationView.FooterMenuItemsSource))
			.FirstOrDefault(x => x.ViewModelType == vmType);

		if (item is null || ReferenceEquals(_navigationView.SelectedItem, item))
		{
			return;
		}

		_isSyncingSelection = true;
		try
		{
			_navigationView.SelectedItem = item;
		}
		finally
		{
			_isSyncingSelection = false;
		}
	}

	private static IEnumerable<NavigationItemViewModel> EnumerateItems(object? source)
	{
		return source is System.Collections.IEnumerable items ? items.OfType<NavigationItemViewModel>() : [];
	}

	protected override Frame GetFrame()
	{
		return _navigationView.FindDescendant<Frame>(x => x.Name == "Main") ?? throw new Exception("Frame not found");
	}
}
