using Microsoft.Win32;

namespace Totoro.Helpers;

/// <summary>
/// Whether Totoro starts when the user signs in to Windows: its entry in the user's Run key, which Task Manager's Startup
/// apps lists too (and can turn off).
/// </summary>
internal static class WindowsStartup
{
	private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string ValueName = "Totoro";
	public const string Argument = "--startup";

	/// <summary>Whether Windows started this instance (from the Run key), rather than the user.</summary>
	public static bool Launched { get; } = Environment.GetCommandLineArgs().Contains(Argument, StringComparer.OrdinalIgnoreCase);

	/// <summary>Read from the registry each time, so it's what Windows will do, whoever changed it.</summary>
	public static bool IsEnabled
	{
		get
		{
			using var key = Registry.CurrentUser.OpenSubKey(RunKey);
			return key?.GetValue(ValueName) is string { Length: > 0 };
		}
		set
		{
			using var key = Registry.CurrentUser.CreateSubKey(RunKey);
			if (value)
			{
				// This exe, as it's running now: quoted, for a path with spaces.
				key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {Argument}");
			}
			else
			{
				key.DeleteValue(ValueName, throwOnMissingValue: false);
			}
		}
	}
}
