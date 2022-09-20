namespace Totoro.Core.Models;

public class WeeklyScheduleModel
{
    public ScheduleModel Monday { get; } = new ScheduleModel { DisplayText = "Mon", DayOfWeek = DayOfWeek.Monday };
    public ScheduleModel Tuesday { get; } = new ScheduleModel { DisplayText = "Tue", DayOfWeek = DayOfWeek.Tuesday };
    public ScheduleModel Wednesday { get; } = new ScheduleModel { DisplayText = "Wed", DayOfWeek = DayOfWeek.Wednesday };
    public ScheduleModel Thursday { get; } = new ScheduleModel { DisplayText = "Thu", DayOfWeek = DayOfWeek.Thursday };
    public ScheduleModel Friday { get; } = new ScheduleModel { DisplayText = "Fri", DayOfWeek = DayOfWeek.Friday};
    public ScheduleModel Saturday { get; } = new ScheduleModel { DisplayText = "Sat", DayOfWeek = DayOfWeek.Saturday };
    public ScheduleModel Sunday { get; } = new ScheduleModel { DisplayText = "Sun", DayOfWeek = DayOfWeek.Sunday };

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

    public List<ScheduleModel> ToList() =>
        new List<ScheduleModel> { Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday }.Where(x => x.Count > 0).ToList();
}
