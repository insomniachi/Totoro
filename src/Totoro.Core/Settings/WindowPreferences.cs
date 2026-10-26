namespace Totoro.Core.Settings;

/// <summary>What closing the main window does.</summary>
public enum WindowCloseAction
{
	Quit,
	/// <summary>Minimizes it to the taskbar; Totoro keeps running.</summary>
	Minimize,
	/// <summary>Hides it, with an icon in the notification area to open it again; Totoro keeps running.</summary>
	Tray,
}

public static class WindowPreferences
{
	private const string CloseActionKey = "WindowCloseAction";
	private const string StartMaximizedKey = "StartMaximized";
	private const string StartInTrayKey = "StartInTray";

	extension(IPreferences preferences)
	{
		public WindowCloseAction CloseAction
		{
			get => preferences.ReadKey(CloseActionKey, WindowCloseAction.Quit);
			set => preferences.SaveKey(CloseActionKey, value);
		}

		/// <summary>Whether the main window opens maximized.</summary>
		public bool StartMaximized
		{
			get => preferences.ReadKey(StartMaximizedKey, false);
			set => preferences.SaveKey(StartMaximizedKey, value);
		}

		/// <summary>Whether Totoro starts hidden, with only its icon in the notification area.</summary>
		public bool StartInTray
		{
			get => preferences.ReadKey(StartInTrayKey, false);
			set => preferences.SaveKey(StartInTrayKey, value);
		}
	}
}
