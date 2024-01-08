using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Splat;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.ViewModels;
using Windows.Foundation;
using Windows.System;
using WinUIEx;

namespace Totoro.WinUI.Views;

// TODO: Update NavigationViewItem titles and icons in ShellPage.xaml.
public sealed partial class ShellPage : Page, IEnableLogger
{
    public ShellViewModel ViewModel { get; }

    public Version Version { get; } = Assembly.GetExecutingAssembly().GetName().Version;

    public ShellPage()
    {
        ViewModel = App.GetService<ShellViewModel>();
        InitializeComponent();

        ViewModel.NavigationService.Frame = NavigationFrame;
        ViewModel.NavigationViewService.Initialize(NavigationViewControl);
        ViewModel.Initialize();

        App.GetService<IWindowService>()
            .IsFullWindowChanged
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(isFullWindow =>
            {
                var presenterKind = isFullWindow ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Overlapped;
                App.MainWindow.AppWindow.SetPresenter(presenterKind);
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

        App.MainWindow.AppWindow.Changed += AppWindow_Changed;
        double scaleAdjustment = NavigationViewControl.XamlRoot.RasterizationScale;
        var transform = NavigationViewControl.TransformToVisual(null);
        var bounds = transform.TransformBounds(new Rect(0, 0, 50, 50));
        InputNonClientPointerSource nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(App.MainWindow.AppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, [GetRect(bounds, scaleAdjustment)]);

        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu));
        KeyboardAccelerators.Add(BuildKeyboardAccelerator(VirtualKey.GoBack));

        var accelerator = new KeyboardAccelerator { Key = VirtualKey.S, Modifiers = VirtualKeyModifiers.Menu | VirtualKeyModifiers.Control };
        accelerator.Invoked += (_, _ ) => ViewModel.NavigationService.NavigateTo<SettingsViewModel>();
        KeyboardAccelerators.Add(accelerator);
    }

    private static Windows.Graphics.RectInt32 GetRect(Rect bounds, double scale)
    {
        return new Windows.Graphics.RectInt32(
            _X: (int)Math.Round(bounds.X * scale),
            _Y: (int)Math.Round(bounds.Y * scale),
            _Width: (int)Math.Round(bounds.Width * scale),
            _Height: (int)Math.Round(bounds.Height * scale)
        );
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPresenterChange)
        {
            return;
        }

        try
        {
            switch (sender.Presenter.Kind)
            {
                case AppWindowPresenterKind.FullScreen:
                    // Full screen - hide the custom title bar
                    // and the default system title bar.
                    //Grid.SetRow(NavigationViewControl, 0);
                    //Grid.SetRowSpan(NavigationViewControl, 2);
                    NavigationViewControl.IsPaneVisible = false;
                    break;

                case AppWindowPresenterKind.Overlapped:
                    
                    // Normal - hide the system title bar
                    // and use the custom title bar instead.
                    //Grid.SetRow(NavigationViewControl, 1);
                    //Grid.SetRowSpan(NavigationViewControl, 1);
                    NavigationViewControl.IsPaneVisible = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            this.Log().Fatal(ex);
        }
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

    private void AppTitleBar_BackButtonClick(object sender, RoutedEventArgs e)
    {
        NavigationFrame.GoBack();
    }

    private void AppTitleBar_PaneButtonClick(object sender, RoutedEventArgs e)
    {
        NavigationViewControl.IsPaneOpen = !NavigationViewControl.IsPaneOpen;
    }
}
