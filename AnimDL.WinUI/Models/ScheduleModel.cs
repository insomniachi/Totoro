using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace AnimDL.WinUI.Models;

public class ScheduleModel : ReactiveObject
{
    [Reactive]
    public int Count { get; set; }
    public string UIText { get; init; }
    public string Key { get; init; }
}
