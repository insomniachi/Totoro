namespace Totoro.Modules;

/// <summary>How often a tool Totoro downloads (mpv, rqbit) is checked for an update, in days.</summary>
public enum UpdateInterval
{
	Never = 0,
	Daily = 1,
	Weekly = 7,
	Monthly = 30
}

internal static class UpdateIntervalExtensions
{
	/// <summary>Whether it's time to check again, <paramref name="lastChecked"/> being when it last was.</summary>
	public static bool IsDue(this UpdateInterval interval, DateTime lastChecked, DateTime now)
	{
		return interval != UpdateInterval.Never && now - lastChecked >= TimeSpan.FromDays((int)interval);
	}
}
