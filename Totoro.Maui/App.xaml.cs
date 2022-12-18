using MalApi;
using Totoro.Core.Contracts;
using Totoro.Maui.Views;

namespace Totoro.Maui
{
    public partial class App : Application
    {
        public App(ILocalSettingsService localSettingsService,
                   Func<LoginMALPage> createLoginPage,
                   INavigationService navigationService)
        {
            InitializeComponent();

            if (localSettingsService.ReadSetting<OAuthToken>("MalToken") is null)
            {
                MainPage = createLoginPage();
            }
            else
            {
                MainPage = new AppShell(navigationService);
            }
        }
    }
}