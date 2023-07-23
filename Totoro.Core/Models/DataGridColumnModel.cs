namespace Totoro.Core.Models;

public class DataGridColumnModel : ReactiveObject
{
    public string Name { get; set; }
    [Reactive] public bool IsVisible { get; set; } = true;
    [Reactive] public int DisplayIndex { get; set; }
    [Reactive] public double? Width { get; set; }
}