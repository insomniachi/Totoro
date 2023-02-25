namespace Totoro.WinUI.Dialogs.ViewModels;

public abstract class DialogViewModel : ReactiveObject, IClosable
{
    protected readonly ScheduledSubject<Unit> _close = new(RxApp.MainThreadScheduler);
    public IObservable<Unit> Close => _close;

    protected void CloseDialog() => _close.OnNext(Unit.Default);

    [Reactive] public string Title { get; set; }
}
