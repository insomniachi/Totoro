using LibVLCSharp.Shared;

namespace Totoro.Modules.MediaPlayers.Vlc;

/// <summary>
/// Loads libvlc (from libvlc\win-x64 next to the app) ahead of the player window, so the window opens straight away.
/// The first load after an install or update can take many seconds, mostly Windows reading its ~400 plugins; later
/// ones are quick, quicker still with the plugin cache (plugins.dat) written by the first.
/// </summary>
internal static class LibVlcLoader
{
	private static readonly Lazy<Task> s_loading = new(() => Task.Run(LoadPlugins));

	public static bool IsLoaded => s_loading.IsValueCreated && s_loading.Value.IsCompleted;

	/// <summary>Starts loading libvlc in the background, if it isn't already.</summary>
	public static void Preload() => _ = s_loading.Value;

	/// <summary>Loads libvlc, waiting for it; on a background thread.</summary>
	public static void Load() => s_loading.Value.GetAwaiter().GetResult();

	private static void LoadPlugins()
	{
		LibVLCSharp.Shared.Core.Initialize();
		var cache = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64", "plugins", "plugins.dat");
		// Scans the plugins once; with no cache yet, saves one (where the app's folder can be written to).
		using var libVlc = File.Exists(cache) ? new LibVLC(enableDebugLogs: false) : new LibVLC(enableDebugLogs: false, "--reset-plugins-cache");
	}
}
