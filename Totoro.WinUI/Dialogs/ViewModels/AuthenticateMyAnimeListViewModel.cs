using System.Web;
using MalApi;
using MalApi.Interfaces;
using Totoro.Core;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class AuthenticateMyAnimeListViewModel : DialogViewModel
{
    public AuthenticateMyAnimeListViewModel(IConfiguration configuration,
                                            ILocalSettingsService localSettingsService,
                                            INavigationService navigationService,
                                            IMalClient malClient,
                                            IMessageBus messageBus)
    {
        var clientId = configuration["ClientId"];
        AuthUrl = MalAuthHelper.GetAuthUrl(clientId);

        this.ObservableForProperty(x => x.AuthUrl, x => x)
            .Where(url => url.Contains("code"))
            .Do(_ => IsLoading = true)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Select(query => HttpUtility.ParseQueryString(query)[0])
            .SelectMany(code => MalAuthHelper.DoAuth(clientId, code))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(token =>
            {
                IsAuthenticated = true;
                localSettingsService.SaveSetting("MalToken", token);
                IsLoading = false;
                _close.OnNext(Unit.Default);
                malClient.SetAccessToken(token.AccessToken);
                messageBus.SendMessage(new MalAuthenticatedMessage());
                navigationService.NavigateTo<UserListViewModel>();
            });
    }

    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public string AuthUrl { get; set; }
    [Reactive] public bool IsAuthenticated { get; set; }
}

public interface IClosable
{
    IObservable<Unit> Close { get; }
}
