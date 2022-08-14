
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Linq;

namespace AnimDL.WinUI.Models;

public class WeeklyScheduleModel
{
    public ScheduleModel Monday { get; } = new ScheduleModel { DisplayText = "Mon", Day = "monday" };
    public ScheduleModel Tuesday { get; } = new ScheduleModel { DisplayText = "Tue", Day = "tuesday" };
    public ScheduleModel Wednesday { get; } = new ScheduleModel { DisplayText = "Wed", Day = "wednesday" };
    public ScheduleModel Thursday { get; } = new ScheduleModel { DisplayText = "Thu", Day = "thursday" };
    public ScheduleModel Friday { get; } = new ScheduleModel { DisplayText = "Fri", Day = "friday" };
    public ScheduleModel Saturday { get; } = new ScheduleModel { DisplayText = "Sat", Day = "saturday" };
    public ScheduleModel Sunday { get; } = new ScheduleModel { DisplayText = "Sun", Day = "wednesday" };

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
