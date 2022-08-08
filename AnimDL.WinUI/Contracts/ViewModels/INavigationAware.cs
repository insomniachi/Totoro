using System.Collections.Generic;
using System.Threading.Tasks;

namespace AnimDL.WinUI.Contracts.ViewModels;

public interface INavigationAware
{
    Task OnNavigatedTo(IReadOnlyDictionary<string,object> parameters);

    Task OnNavigatedFrom();
}
