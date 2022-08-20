namespace AnimDL.UI.Core.Contracts;

public interface INavigationAware
{
    Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters);

    Task OnNavigatedFrom();
}
