namespace Totoro.Core.Models;

public class DataGridColumnModel : ReactiveObject
{
    public string Name { get; set; }
    [Reactive] public bool IsVisible { get; set; } = true;
    [Reactive] public int DisplayIndex { get; set; }
    [Reactive] public double Width { get; set; }

    public DataGridColumnModel()
    {
        //this.WhenAnyValue(x => x.IsVisible)
        //    .ObserveOn(RxApp.MainThreadScheduler)
        //    .Subscribe(visible =>
        //    {
        //        if (visible)
        //        {
        //            Width = LastKnownWidth;
        //        }
        //        else if( Width > 0)
        //        {
        //            LastKnownWidth = Width;
        //            Width = 0;
        //        }
        //    });

        //this.WhenAnyValue(x => x.Width)
        //    .DistinctUntilChanged()
        //    .Where(_ => IsVisible && Width > 0)
        //    .Subscribe(_ =>
        //    {
        //        LastKnownWidth = Width;
        //    });

    }
}