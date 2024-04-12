using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using Microsoft.Extensions.DependencyInjection;
using Totoro.Avalonia.Contracts;
using Totoro.Core.Contracts;

namespace Totoro.Avalonia.Views
{
    public partial class MainWindow : AppWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            var navigationViewService = App.Services.GetRequiredService<INavigationViewService>();
            var navigationService = App.Services.GetRequiredService<IAvaloniaNavigationService>();
            
            navigationViewService.Initialize(NavigationView);
            navigationService.SetFrame(FrameView);
        }
    }
}