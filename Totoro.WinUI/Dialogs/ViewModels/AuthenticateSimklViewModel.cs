using System.Web;
using Flurl.Http;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class AuthenticateSimklViewModel : DialogViewModel
{
    private readonly string _redirectUri = @"https://github.com/insomniachi/Totoro/";

    public AuthenticateSimklViewModel(IConfiguration configuration,
                                      ILocalSettingsService localSettingsService,
                                      INavigationService navigationService,
                                      ITrackingServiceContext trackingService,
                                      ISettings settings)
    {
        var clientId = configuration["ClientIdSimkl"];
        AuthUrl = $"https://simkl.com/oauth/authorize?response_type=code&client_id={clientId}&redirect_uri={_redirectUri}";

        this.ObservableForProperty(x => x.AuthUrl, x => x)
            .Select(HttpUtility.ParseQueryString)
            .Where(queries => queries.Count > 0 && queries.Keys[0]?.EndsWith("code") == true)
            .Do(_ => IsLoading = true)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .SelectMany(async queries =>
            {
                var json = await "https://api.simkl.com/oauth/token".PostJsonAsync(new
                {
                    code = queries[0].ToString(),
                    client_id = clientId,
                    client_secret = "a8e1ace06188a964a50b9b87e3fe659d45530953d848d65f69a80fdaf53def9e",
                    redirect_uri = "github.com/insomniachi/totoro/",
                    grant_type = "authorization_code"
                }).ReceiveJson();

                return (string)json.access_token;
            })
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(token =>
            {
                IsAuthenticated = true;
                localSettingsService.SaveSetting("SimklToken", token);
                IsLoading = false;
                CloseDialog();
                trackingService.SetAccessToken(token, ListServiceType.Simkl);
                settings.DefaultListService = ListServiceType.Simkl;
                navigationService.NavigateTo<UserListViewModel>();
            });
    }

    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public string AuthUrl { get; set; }
    [Reactive] public bool IsAuthenticated { get; set; }
}
