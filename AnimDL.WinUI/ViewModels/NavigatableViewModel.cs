using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using AnimDL.WinUI.Contracts.ViewModels;
using ReactiveUI;

namespace AnimDL.WinUI.ViewModels;

public abstract class NavigatableViewModel : ReactiveObject, INavigationAware, IDisposable
{
    private bool _disposed;
    public CompositeDisposable Garbage { get; } = new CompositeDisposable();

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Garbage.Dispose();
            }

            // There are no unmanaged resources to release, but
            // if we add them, they need to be released here.
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual Task OnNavigatedFrom()
    {
        return Task.CompletedTask;
    }

    public virtual Task OnNavigatedTo(IReadOnlyDictionary<string, object> parameters) => Task.CompletedTask;
}
