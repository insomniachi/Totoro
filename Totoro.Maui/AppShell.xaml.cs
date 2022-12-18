using Totoro.Core.Contracts;
using Totoro.Maui.Services;

namespace Totoro.Maui
{
    public partial class AppShell : Shell
    {
        private readonly INavigationService _navigationService;

        public AppShell(INavigationService navigationService)
        {
            InitializeComponent();
            _navigationService = navigationService;
        }

        private void Shell_Loaded(object sender, EventArgs e)
        {
            ((ShellNavigationService)_navigationService).Initialize();
        }
    }
}