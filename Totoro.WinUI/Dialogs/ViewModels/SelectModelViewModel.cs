namespace Totoro.WinUI.Dialogs.ViewModels;

public class SelectModelViewModel<TModel> : DialogViewModel, ISelectModelViewModel
{
    public SelectModelViewModel()
    {
        this.WhenAnyValue(x => x.SearchText)
            .Where(x => x is { Length: > 2 })
            .Throttle(TimeSpan.FromMilliseconds(200))
            .SelectMany(text => Search?.Invoke(text))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => Models = x, RxApp.DefaultExceptionHandler.OnError);
    }

    [Reactive] public IEnumerable<TModel> Models { get; set; }
    [Reactive] public TModel SelectedModel { get; set; }
    [Reactive] public string SearchText { get; set; }
    public Func<string, IObservable<IEnumerable<TModel>>> Search { get; set; }

    IEnumerable<object> ISelectModelViewModel.Models => (IEnumerable<object>)Models;
    object ISelectModelViewModel.SelectedModel
    {
        get => SelectedModel;
        set
        {
            if (value is not TModel model)
            {
                return;
            }
            SelectedModel = model;
        }
    }
}

public interface ISelectModelViewModel
{
    IEnumerable<object> Models { get; }
    object SelectedModel { get; set; }
    string SearchText { get; set; }
}
