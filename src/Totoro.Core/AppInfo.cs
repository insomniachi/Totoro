using System.Reflection;

namespace Totoro.Core;

public static class AppInfo
{
	/// <summary>
	/// The app's version, e.g. 2.1.0: the release's (see .github/workflows), without the commit's hash. 0.0 when it isn't
	/// known.
	/// </summary>
	public static Version Version { get; internal set; } = GetVersion();

	private static Version GetVersion()
	{
		var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
		return System.Version.TryParse(version.Split('+', '-')[0], out var parsed) ? parsed : new Version(0, 0);
	}

	/// <summary>
	/// Whether this version of the app is <paramref name="minimum"/> (e.g. a plugin's minAppVersion) or later. A version
	/// that can't be read, the app's or the minimum's, doesn't stop anything.
	/// </summary>
	public static bool IsAtLeast(string? minimum) =>
		Version == new Version(0, 0) || !System.Version.TryParse(minimum, out var required) || Version >= required;
}
