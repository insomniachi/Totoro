using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using AnimDL.WinUI.Contracts.ViewModels;
using ReactiveUI;

namespace AnimDL.WinUI.ViewModels;

public abstract class ViewModel : ReactiveObject, INavigationAware
{
    protected CompositeDisposable Garbage { get; } = new CompositeDisposable();
    public virtual Task OnNavigatedFrom()
    {
        Garbage.Dispose();
        return Task.CompletedTask;
    }
    public virtual Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters) => Task.CompletedTask;
}
