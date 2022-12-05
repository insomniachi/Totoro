namespace Totoro.Core.Contracts;

public interface ISystemClock
{
    public DateTime Today { get; }
    public DateTime Now { get; }
    public DateTime UtcNow { get; }
}
