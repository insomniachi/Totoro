namespace Totoro.Core.Services;

public class SystemClock : ISystemClock
{
    public DateTime Today => DateTime.Today;
    public DateTime Now => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;
}
