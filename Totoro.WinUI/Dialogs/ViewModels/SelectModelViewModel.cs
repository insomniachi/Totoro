using System.Reactive.Threading.Tasks;

namespace Totoro.WinUI.Dialogs.ViewModels;

public class SelectModelViewModel<TModel> : DialogViewModel, ISelectModelViewModel
{
    public SelectModelViewModel()
    {
        this.WhenAnyValue(x => x.SearchText)
            .WhereNotNull()
            .SelectMany(text => Search?.Invoke(text))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(x => Models = x);

    }

    [Reactive] public IEnumerable<TModel> Models { get; set; }
    [Reactive] public TModel SelectedModel { get; set; }
    [Reactive] public string SearchText { get; set; }
    public Func<string,Task<IEnumerable<TModel>>> Search { get; set; }

    IEnumerable<object> ISelectModelViewModel.Models => (IEnumerable<object>)Models;
    object ISelectModelViewModel.SelectedModel
    {
        get => SelectedModel;
        set
        {
            if(value is not TModel model)
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
