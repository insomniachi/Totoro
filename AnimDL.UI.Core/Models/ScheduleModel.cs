namespace AnimDL.UI.Core.Models;

public class ScheduleModel : ReactiveObject
{
    [Reactive]
    public int Count { get; set; }
    public string DisplayText { get; init; }
    public string Day { get; init; }
}
