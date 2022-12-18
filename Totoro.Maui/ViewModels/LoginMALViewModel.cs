using System.Reactive.Linq;
using System.Web;
using MalApi;
using MalApi.Interfaces;
using Microsoft.Extensions.Configuration;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Totoro.Core;
using Totoro.Core.Contracts;
using Totoro.Core.ViewModels;

namespace Totoro.Maui.ViewModels;

public class LoginMALViewModel : ReactiveObject
{
    public LoginMALViewModel(IConfiguration configuration,
                             ILocalSettingsService localSettingsService,
                             INavigationService navigationService,
                             IMalClient malClient)
    {
        var clientId = configuration["ClientId"];
        AuthUrl = MalAuthHelper.GetAuthUrl(clientId);

        this.ObservableForProperty(x => x.AuthUrl, x => x)
            .Where(url => url.Contains("code"))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Select(query => HttpUtility.ParseQueryString(query)[0])
            .SelectMany(code => MalAuthHelper.DoAuth(clientId, code))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(token =>
            {
                localSettingsService.SaveSetting("MalToken", token);
                malClient.SetAccessToken(token.AccessToken);
                MessageBus.Current.SendMessage(new MalAuthenticatedMessage());
                navigationService.NavigateTo<UserListViewModel>();
            });
    }

    private string _authUrl;
    public string AuthUrl
    {
        get => _authUrl;
        set => this.RaiseAndSetIfChanged(ref _authUrl, value);
    }
}
