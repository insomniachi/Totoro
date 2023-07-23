namespace Totoro.Core.Models;

public class DataGridSettings : ReactiveObject
{
    [Reactive] public DataGridSort Sort { get; set; } = new("Title", true);
    public List<DataGridColumnModel> Columns { get; init; }

    public IObservable<DataGridColumnModel> OnColumnChanged()
    {
        var observables = Columns.Select(x => x.WhenAnyPropertyChanged());
        return Observable.Merge(observables).Throttle(TimeSpan.FromSeconds(1));
    }

    public IObservable<DataGridColumnModel> OnColumnVisibilityChanged()
    {
        return Observable.Merge(Columns.Select(x => x.WhenAnyPropertyChanged(nameof(DataGridColumnModel.IsVisible))));
    }
}
