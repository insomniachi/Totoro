using System.Diagnostics;
using DynamicData;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Helpers;
using Totoro.WinUI.Services;
using Totoro.WinUI.ViewModels;
using Windows.System;
using WinUIEx;

namespace Totoro.WinUI.Views;

// TODO: Update NavigationViewItem titles and icons in ShellPage.xaml.
public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel { get; }

    public ShellPage()
    {
        ViewModel = App.GetService<ShellViewModel>();
        InitializeComponent();

        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);
        ViewModel.Initialize();

        App.GetService<IWindowService>()
            .IsFullWindowChanged
            .Subscribe(isFullWindow =>
            {
                App.MainWindow.PresenterKind = isFullWindow ? Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen : Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped;
                NavigationViewControl.IsPaneVisible = !isFullWindow;
            });

        ShowHideWindowCommand = ReactiveCommand.Create(ShowHideWindow);
        ExitApplicationCommand = ReactiveCommand.Create(ExitApplication);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.HandleClosedEvents = App.GetService<ISettings>().StartupOptions.MinimizeToTrayOnClose;
        App.MainWindow.Closed += (sender, args) =>
        {
            if (App.HandleClosedEvents)
            {
                args.Handled = true;
                App.MainWindow.Hide();
            }
            else
            {
                TrayIcon?.Dispose();
            }
        };

        App.MainWindow.ExtendsContentIntoTitleBar = true;

        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));

        var accelerator = new KeyboardAccelerator { Key = VirtualKey.S, Modifiers = VirtualKeyModifiers.Menu | VirtualKeyModifiers.Control };
        accelerator.Invoked += (_, _ ) => ViewModel.NavigationService.NavigateTo<SettingsViewModel>();
        KeyboardAccelerators.Add(accelerator);
    }

    private void NavigationViewControl_DisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        AppTitleBar.Margin = new Thickness()
        {
            Left = sender.CompactPaneLength * (sender.DisplayMode == NavigationViewDisplayMode.Minimal ? 2 : 1),
            Top = AppTitleBar.Margin.Top,
            Right = AppTitleBar.Margin.Right,
            Bottom = AppTitleBar.Margin.Bottom
        };
    }

    private static KeyboardAccelerator BuildKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers? modifiers = null)
    {
        var keyboardAccelerator = new KeyboardAccelerator() { Key = key };

        if (modifiers.HasValue)
        {
            keyboardAccelerator.Modifiers = modifiers.Value;
        }

        keyboardAccelerator.Invoked += OnKeyboardAcceleratorInvoked;

        return keyboardAccelerator;
    }

    private static void OnKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var navigationService = App.GetService<INavigationService>();

        var result = navigationService.GoBack();

        args.Handled = result;
    }

    private void Feedback_Tapped(object sender, TappedRoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(@"https://github.com/insomniachi/Totoro/issues")
        {
            UseShellExecute = true
        });
    }

    public ICommand ShowHideWindowCommand { get; }
    public ICommand ExitApplicationCommand { get; }

    public static void ShowHideWindow()
    {
        var window = App.MainWindow;
        if (window == null)
        {
            return;
        }

        if (window.Visible)
        {
            window.Hide();
        }
        else
        {
            window.Show();
        }
    }

    public void ExitApplication()
    {
        App.HandleClosedEvents = false;
        TrayIcon.Dispose();
        App.MainWindow?.Close();
    }
}
