using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.Navigation;

namespace Totoro.UI.Navigation;

internal abstract class FrameBasedNavigator(IViewRegistry viewRegistry, IServiceScopeFactory serviceScopeFactory, ILogger logger) : INavigator
{
	public event EventHandler? Navigated;

	public Task NavigateToData(object data)
	{
		try
		{
			var map = viewRegistry.FindByData(data.GetType());

			if (map is not { View: { } viewType, ViewModel: { } vmType })
			{
				return Task.CompletedTask;
			}

			Navigate(map, data);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Navigation failed");
			return Task.CompletedTask;
		}

		return Task.CompletedTask;
	}

	public Task NavigateToKey(string key, object? data = null)
	{
		try
		{
			var map = viewRegistry.FindByKey(key);

			if (map is not { View: { } viewType, ViewModel: { } vmType })
			{
				return Task.CompletedTask;
			}

			Navigate(map, data);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Navigation failed");
			return Task.CompletedTask;
		}

		return Task.CompletedTask;
	}

	public Task NavigateToViewModel(Type viewModelType, object? data = null)
	{
		try
		{
			var map = viewRegistry.FindByViewModel(viewModelType);

			if (map is not { View: { } viewType, ViewModel: { } vmType })
			{
				return Task.CompletedTask;
			}

			Navigate(map, data);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Navigation failed");
			return Task.CompletedTask;
		}

		return Task.CompletedTask;
	}

	protected async void OnFrameNavigating(object sender, NavigatingCancelEventArgs e)
	{
		try
		{
			if (GetFrame().Content is not Page page)
			{
				return;
			}

			if (page.DataContext is INavigationAware navigationAware)
			{
				await navigationAware.OnNavigatedFrom();
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "ViewModel Uninitialization Failed");
		}
	}

	protected async void OnFrameNavigated(object sender, NavigationEventArgs e)
	{
		try
		{
			if (GetFrame().Content is not Page page)
			{
				return;
			}

			var type = e.SourcePageType;

			if (viewRegistry.FindByView(type) is not { } map)
			{
				return;
			}

			using var scope = serviceScopeFactory.CreateScope();
			var vmObj = e.Parameter is null
				? ActivatorUtilities.CreateInstance(scope.ServiceProvider, map.ViewModel)
				: ActivatorUtilities.CreateInstance(scope.ServiceProvider, map.ViewModel, e.Parameter);
			page.DataContext = vmObj;
			type.GetProperty("ViewModel")?.SetValue(page, vmObj);

			if (vmObj is INavigationAware navigationAware)
			{
				await navigationAware.OnNavigatedTo();
			}

		}
		catch (Exception ex)
		{
			logger.LogError(ex, "ViewModel Initialization Failed");
		}
	}

	protected void Navigate(ViewMap map, object? data)
	{
		OnNavigating();
		GetFrame().Navigate(map.View, data);
		OnNavigated();
	}

	protected abstract Frame GetFrame();

	protected void OnNavigated() => Navigated?.Invoke(this, EventArgs.Empty);

	protected virtual void OnNavigating() { }
}
