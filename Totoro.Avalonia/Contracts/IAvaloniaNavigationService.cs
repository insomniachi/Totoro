using System;
using System.Collections.Generic;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using FluentAvalonia.UI.Navigation;
using Totoro.Core.Contracts;

namespace Totoro.Avalonia.Contracts;

public interface IAvaloniaNavigationService : INavigationService
{
    IObservable<NavigationEventArgs> Navigated { get; }

    bool NavigateTo(Type viewModelType, object? viewModel = null, IReadOnlyDictionary<string, object>? parameter = null,
        bool clearNavigation = false, NavigationTransitionInfo? transitionInfo = null);

    void SetFrame(Frame frame);

    bool HasFrame();

    void Dispose();
}