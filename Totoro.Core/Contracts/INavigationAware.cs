namespace Totoro.Core.Contracts;

public interface INavigationAware
{
    Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters);

    Task OnNavigatedFrom();
}

public interface IHandleNavigation
{
    void GoBack();
    bool CanHandle();
}
