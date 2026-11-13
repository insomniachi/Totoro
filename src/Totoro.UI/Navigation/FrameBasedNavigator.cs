using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Navigation;

namespace Totoro.UI.Navigation;

internal abstract class FrameBasedNavigator : INavigator, IRecipient<AnimeDatabaseChangedMessage>
{
	private readonly IViewRegistry viewRegistry;
	private readonly IServiceScopeFactory serviceScopeFactory;
	private readonly ILogger logger;

	// Scope that owns the current page's view model and its dependencies.
	// Disposed when we navigate away, after OnNavigatedFromAsync has run.
	private IServiceScope? _currentScope;

	// What the current page was given, to make it again with.
	private object? _currentParameter;

	protected FrameBasedNavigator(IViewRegistry viewRegistry, IServiceScopeFactory serviceScopeFactory, ILogger logger)
	{
		this.viewRegistry = viewRegistry;
		this.serviceScopeFactory = serviceScopeFactory;
		this.logger = logger;
		// Navigators live as long as the window (Navigators keeps them).
		WeakReferenceMessenger.Default.Register(this);
	}

	// Sent from wherever the database was picked (Settings, the title bar, an agent tool): handled on the UI thread.
	public void Receive(AnimeDatabaseChangedMessage message) => GetFrame().DispatcherQueue.TryEnqueue(() =>
	{
		var frame = GetFrame();
		if (frame.Content is not Page { DataContext: IAnimeDatabaseView })
		{
			return;
		}

		if (this is IDismissibleNavigator dismissible)
		{
			dismissible.Close();
			return;
		}

		// The same page again, with a new view model, in place of the old one in the history.
		frame.Navigate(frame.Content.GetType(), _currentParameter);
		if (frame.BackStackDepth > 0)
		{
			frame.BackStack.RemoveAt(frame.BackStackDepth - 1);
		}

		OnNavigated();
	});

	public event EventHandler? Navigated;

	public bool CanGoBack => GetFrame().CanGoBack;

	public Task GoBackAsync()
	{
		try
		{
			var frame = GetFrame();
			if (!frame.CanGoBack)
			{
				return Task.CompletedTask;
			}

			frame.GoBack();
			OnWentBack();
			OnNavigated();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Back navigation failed");
		}

		return Task.CompletedTask;
	}

	public Task NavigateToDataAsync(object data)
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

	public Task NavigateToKeyAsync(string key, object? data = null)
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

	public Task NavigateToViewModelAsync(Type viewModelType, object? data = null)
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
		if (e.Cancel)
		{
			return;
		}

		var outgoingScope = _currentScope;
		_currentScope = null;

		try
		{
			if (GetFrame().Content is Page { DataContext: INavigationAware navigationAware })
			{
				await navigationAware.OnNavigatedFromAsync();
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "ViewModel Uninitialization Failed");
		}
		finally
		{
			outgoingScope?.Dispose();
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

			// Not disposed here: the view model keeps using services from this scope
			// for as long as the page is shown. OnFrameNavigating disposes it.
			var scope = serviceScopeFactory.CreateScope();
			_currentScope = scope;
			_currentParameter = e.Parameter;
			var vmObj = e.Parameter is null
				? ActivatorUtilities.CreateInstance(scope.ServiceProvider, map.ViewModel)
				: ActivatorUtilities.CreateInstance(scope.ServiceProvider, map.ViewModel, e.Parameter);
			page.DataContext = vmObj;
			type.GetProperty("ViewModel")?.SetValue(page, vmObj);

			if (vmObj is INavigationAware navigationAware)
			{
				await navigationAware.OnNavigatedToAsync();
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

	/// <summary>
	/// Leaves the current page without showing a new one: the view model gets OnNavigatedFromAsync,
	/// its scope is disposed, and the frame's content and history are cleared.
	/// </summary>
	protected async Task ClearAsync()
	{
		var outgoingScope = _currentScope;
		_currentScope = null;

		try
		{
			var frame = GetFrame();
			if (frame.Content is Page { DataContext: INavigationAware navigationAware })
			{
				await navigationAware.OnNavigatedFromAsync();
			}

			frame.Content = null;
			frame.BackStack.Clear();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Clearing navigation failed");
		}
		finally
		{
			outgoingScope?.Dispose();
		}

		OnNavigated();
	}

	/// <summary>Called after a successful GoBack, once the previous page is showing again.</summary>
	protected virtual void OnWentBack() { }

	/// <summary>The view model type of the page currently shown in the frame, if it's registered.</summary>
	protected Type? GetCurrentViewModelType()
	{
		return GetFrame().Content is { } content ? viewRegistry.FindByView(content.GetType())?.ViewModel : null;
	}
}
