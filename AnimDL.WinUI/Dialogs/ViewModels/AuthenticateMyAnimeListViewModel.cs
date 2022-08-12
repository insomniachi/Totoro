using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Web;
using AnimDL.WinUI.Contracts.Services;
using AnimDL.WinUI.ViewModels;
using MalApi;
using MalApi.Interfaces;
using Microsoft.Extensions.Configuration;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AnimDL.WinUI.Dialogs.ViewModels;

public class AuthenticateMyAnimeListViewModel : ReactiveObject, IClosable
{
    private readonly ScheduledSubject<Unit> _close = new(RxApp.MainThreadScheduler);

    public AuthenticateMyAnimeListViewModel(IConfiguration configuration,
                                            ILocalSettingsService localSettingsService,
                                            INavigationService navigationService,
                                            IMalClient malClient)
    {
        var clientId = configuration["ClientId"];
        AuthUrl = MalAuthHelper.GetAuthUrl(clientId);

        this.ObservableForProperty(x => x.AuthUrl, x => x)
            .Where(x => x.Contains("code"))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async x =>
            {
                IsLoading = true;
                IsAuthenticated = true;
                var code = HttpUtility.ParseQueryString(x)[0];
                var token = await MalAuthHelper.DoAuth(clientId, code);
                localSettingsService.SaveSetting("MalToken", token);
                IsLoading = false;
                _close.OnNext(Unit.Default);
                malClient.SetAccessToken(token.AccessToken);
                App.GetService<ShellViewModel>().IsAuthenticated = true;
                navigationService.NavigateTo<UserListViewModel>();
            });
    }

    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public string AuthUrl { get; set; }
    [Reactive] public bool IsAuthenticated { get; set; }

    public IObservable<Unit> Close => _close;
}

public interface IClosable
{
    IObservable<Unit> Close { get; }
}
