
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;

namespace AnimDL.WinUI.Models;

public class WeeklyScheduleModel
{
    public ScheduleModel Monday { get; } = new ScheduleModel { UIText = "Mon", Key = "monday" };
    public ScheduleModel Tuesday { get; } = new ScheduleModel { UIText = "Tue", Key = "tuesday" };
    public ScheduleModel Wednesday { get; } = new ScheduleModel { UIText = "Wed", Key = "wednesday" };
    public ScheduleModel Thursday { get; } = new ScheduleModel { UIText = "Thu", Key = "thursday" };
    public ScheduleModel Friday { get; } = new ScheduleModel { UIText = "Fri", Key = "friday" };
    public ScheduleModel Saturday { get; } = new ScheduleModel { UIText = "Sat", Key = "saturday" };
    public ScheduleModel Sunday { get; } = new ScheduleModel { UIText = "Sun", Key = "wednesday" };

    public ScheduleModel this[DayOfWeek day]
    {
        get => day switch
        {
            DayOfWeek.Monday => Monday,
            DayOfWeek.Tuesday => Tuesday,
            DayOfWeek.Wednesday => Wednesday,
            DayOfWeek.Thursday => Thursday,
            DayOfWeek.Friday => Friday,
            DayOfWeek.Saturday => Saturday,
            DayOfWeek.Sunday => Sunday,
            _ => throw new ArgumentException("invalid", nameof(day))
        };
    }

    public IEnumerable<ScheduleModel> ToList() =>
        new List<ScheduleModel> { Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday }.Where(x => x.Count > 0);
}
