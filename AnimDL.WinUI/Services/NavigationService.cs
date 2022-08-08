using System.Collections.Generic;
using AnimDL.WinUI.Contracts.Services;
using AnimDL.WinUI.Contracts.ViewModels;
using AnimDL.WinUI.Core.Contracts;
using AnimDL.WinUI.Core.Contracts.Services;
using AnimDL.WinUI.Helpers;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AnimDL.WinUI.Services;

// For more information on navigation between pages see
// https://github.com/microsoft/TemplateStudio/blob/main/docs/WinUI/navigation.md
public class NavigationService : INavigationService
{
    private readonly IPageService _pageService;
    private readonly IVolatileStateStorage _stateStorage;
    private object _lastParameterUsed;
    private Frame _frame;

    public event NavigatedEventHandler Navigated;

    public Frame Frame
    {
        get
        {
            if (_frame == null)
            {
                _frame = App.MainWindow.Content as Frame;
                RegisterFrameEvents();
            }

            return _frame;
        }

        set
        {
            UnregisterFrameEvents();
            _frame = value;
            RegisterFrameEvents();
        }
    }

    public bool CanGoBack => Frame.CanGoBack;

    public NavigationService(IPageService pageService, 
                             IVolatileStateStorage stateStorage)
    {
        _pageService = pageService;
        _stateStorage = stateStorage;
    }

    private void RegisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated += OnNavigated;
        }
    }

    private void UnregisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated -= OnNavigated;
        }
    }

    public bool GoBack()
    {
        if (CanGoBack)
        {
            var vmBeforeNavigation = _frame.GetPageViewModel();
            _frame.GoBack();
            if (vmBeforeNavigation is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }

            return true;
        }

        return false;
    }

    public bool NavigateTo(string pageKey, IReadOnlyDictionary<string,object> parameter = null, bool clearNavigation = false)
    {
        var pageType = _pageService.GetPageType(pageKey);

        if (_frame.Content?.GetType() != pageType || (parameter != null && !parameter.Equals(_lastParameterUsed)))
        {
            _frame.Tag = clearNavigation;
            var vmBeforeNavigation = _frame.GetPageViewModel();
            var navigated = _frame.Navigate(pageType, parameter);
            if (navigated)
            {
                _lastParameterUsed = parameter;
                if (vmBeforeNavigation is INavigationAware navigationAware)
                {
                    navigationAware.OnNavigatedFrom();
                }
                if(vmBeforeNavigation is IHaveState stateAware)
                {
                    var state = _stateStorage.GetState(stateAware.GetType());
                    stateAware.StoreState(state);
                }
            }

            return navigated;
        }

        return false;
    }

    private async void OnNavigated(object sender, NavigationEventArgs e)
    {
        if (sender is not Frame frame)
        {
            return;
        }
        
        var clearNavigation = (bool)frame.Tag;
        if (clearNavigation)
        {
            frame.BackStack.Clear();
        }

        var vm = frame.GetPageViewModel();

        if (vm is INavigationAware navigationAware)
        {
            await navigationAware.OnNavigatedTo(e.Parameter as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>());
        }
        
        if (vm is IHaveState stateAware)
        {
            var state = _stateStorage.GetState(stateAware.GetType());

            if(state.IsEmpty)
            {
                await stateAware.SetInitialState();
            }
            else
            {
                stateAware.RestoreState(state);
            }
        }

        Navigated?.Invoke(sender, e);
    }
}
