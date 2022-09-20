namespace Totoro.Core.Models;

public class ScheduleModel : ReactiveObject
{
    [Reactive]
    public int Count { get; set; }
    public string DisplayText { get; init; }
    public DayOfWeek DayOfWeek { get; init; }
}
