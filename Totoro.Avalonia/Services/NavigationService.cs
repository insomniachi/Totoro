using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Subjects;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;
using ReactiveUI;
using Splat;
using Totoro.Avalonia.Contracts;
using Totoro.Avalonia.Helpers;
using Totoro.Core.Contracts;

namespace Totoro.Avalonia.Services;

public class NavigationService : IAvaloniaNavigationService, IEnableLogger
{
    private object? _lastParameterUsed;
    private Frame? _frame;
    private readonly Subject<NavigationEventArgs> _subject = new();

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public bool NavigateTo(Type viewModelType,
        object? viewModel = null,
        IReadOnlyDictionary<string, object>? parameter = null,
        bool clearNavigation = false)
    {
        return NavigateTo(viewModelType, viewModel, parameter, clearNavigation, null);
    }

    public bool NavigateTo<TViewModel>(TViewModel? viewModel = null,
        IReadOnlyDictionary<string, object>? parameter = null,
        bool clearNavigation = false)
        where TViewModel : class, INotifyPropertyChanged
    {
        return NavigateTo(typeof(TViewModel), viewModel, parameter, clearNavigation);
    }

    public bool GoBack()
    {
        _frame?.GoBack();
        return true;
    }

    public IObservable<NavigationEventArgs> Navigated => _subject;


    public void SetFrame(Frame frame)
    {
        _frame = frame;
        _frame.NavigationPageFactory = new NavigationPageFactory();
        _frame.Navigated += OnNavigated;
    }

    public bool HasFrame() => _frame is not null;

    public void Dispose()
    {
        if (_frame is null)
        {
            return;
        }
        
        _frame.Navigated -= OnNavigated;
        _frame = null;
    }

    public bool NavigateTo(Type viewModelType,
        object? viewModel,
        IReadOnlyDictionary<string, object>? parameter = null,
        bool clearNavigation = false,
        NavigationTransitionInfo? transition = null)
    {
        if (_frame is null)
        {
            return false;
        }

        var viewKey = typeof(ViewType<>).MakeGenericType(viewModelType);
        var pageType = ((ViewType?)App.Services.GetService(viewKey))?.Type;

        if (pageType is null)
        {
            return false;
        }

        if (viewModel is not null)
        {
            var @params = parameter is not null
                ? new Dictionary<string, object>(parameter)
                : [];

            @params.Add("ViewModel", viewModel);

            parameter = @params;
        }


        if ((_frame?.Content?.GetType()) == pageType && (parameter == null || parameter.Equals(_lastParameterUsed)))
        {
            return false;
        }

        _frame!.Tag = clearNavigation;

        var vmBeforeNavigation = GetContentViewModel(_frame);

        var navigationOptions = new FrameNavigationOptions();
        if (transition is not null)
        {
            navigationOptions.TransitionInfoOverride = transition;
        }
        
        var navigated = viewModel is null
            ? _frame.NavigateToType(viewModelType, "parameter", navigationOptions)
            : _frame.NavigateFromObject(viewModel, navigationOptions);
        
        if (navigated)
        {
            _lastParameterUsed = parameter;
            if (vmBeforeNavigation is IHaveState stateAware)
            {
                //var state = _stateStorage.GetState(stateAware.GetType());
                //stateAware.StoreState(state);
            }
            if (vmBeforeNavigation is IDisposable disposable)
            {
                disposable.Dispose();
            }
            if (vmBeforeNavigation is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }
        }

        return navigated;
    }

    private async void OnNavigated(object sender, NavigationEventArgs e)
    {
        if (sender is not Frame frame)
        {
            return;
        }

        var parameter = e.Parameter as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>();

        var clearNavigation = (bool?)frame.Tag;
        if (clearNavigation is true)
        {
            frame.BackStack.Clear();
        }

        if (frame.Content is not IViewFor view)
        {
            _subject.OnNext(e);
            return;
        }
        
        view.ViewModel = !parameter.TryGetValue("ViewModel", out var value)
            ? App.Services.GetService(view.GetType().GetProperty(nameof(IViewFor.ViewModel))!.PropertyType)
            : value;

        if (view is Control c)
        {
            c.DataContext = view.ViewModel;
        }

        this.Log().Debug($"Navigated to {view.ViewModel?.GetType().Name}");

        if (view.ViewModel is IHaveState stateAware)
        {
            //var state = _stateStorage.GetState(stateAware.GetType());
        
            //if (state.IsEmpty)
            {
                await stateAware.SetInitialState();
            }
            //else
            //{
             //   stateAware.RestoreState(state);
            //}
        }
        
        if (view.ViewModel is INavigationAware navigationAware)
        {
            try
            {
                await navigationAware.OnNavigatedTo(e.Parameter as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>());
            }
            catch (Exception ex)
            {
                this.Log().Fatal(ex);
            }
        }

        _subject.OnNext(e);
    }

    private static object? GetContentViewModel(ContentControl frame)
    {
        return frame.Content is IViewFor view
            ? view.ViewModel
            : null;
    }
}

public record ViewType<TViewModel>(Type Type) : ViewType(Type)
    where TViewModel : class, INotifyPropertyChanged;

public record ViewType(Type Type);