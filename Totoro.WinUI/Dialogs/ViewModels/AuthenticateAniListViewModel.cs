using System.Web;
using Totoro.Core.Services.AniList;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class AuthenticateAniListViewModel : DialogViewModel
{
    public AuthenticateAniListViewModel(IConfiguration configuration,
                                        ILocalSettingsService localSettingsService,
                                        INavigationService navigationService,
                                        ITrackingServiceContext trackingService,
                                        ISettings settings)
    {
        var clientId = configuration["ClientIdAniList"];
        AuthUrl = $"https://anilist.co/api/v2/oauth/authorize?client_id={clientId}&response_type=token";

        this.ObservableForProperty(x => x.AuthUrl, x => x)
            .Where(url => url.Contains("access_token"))
            .Do(_ => IsLoading = true)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Select(HttpUtility.ParseQueryString)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(queries =>
            {
                IsAuthenticated = true;
                var token = new AniListAuthToken
                {
                    AccessToken = queries[0],
                    ExpiresIn = long.Parse(queries[2]),
                    CreatedAt = DateTime.Now
                };

                localSettingsService.SaveSetting("AniListToken", token);

                IsLoading = false;
                CloseDialog();
                trackingService.SetAccessToken(token.AccessToken, ListServiceType.AniList);
                settings.DefaultListService = ListServiceType.AniList;
                navigationService.NavigateTo<UserListViewModel>();
            });
    }

    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public string AuthUrl { get; set; }
    [Reactive] public bool IsAuthenticated { get; set; }
}
