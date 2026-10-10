namespace Totoro.Core.Settings;

public enum AppTheme
{
	/// <summary>Follow the Windows setting.</summary>
	System,
	Light,
	Dark,
}

public enum AppBackdrop
{
	Mica,
	MicaAlt,
	Acrylic,
	/// <summary>A plain, solid background.</summary>
	None,
}

/// <summary>The app's theme and window backdrop. Setting either saves it and applies it to the window straight away.</summary>
public interface IAppearanceService
{
	AppTheme Theme { get; set; }

	AppBackdrop Backdrop { get; set; }

	/// <summary>Applies the saved theme and backdrop; call once the main window exists.</summary>
	void Apply();
}

public static class AppearancePreferences
{
	private const string ThemeKey = "Theme";
	private const string BackdropKey = "Backdrop";

	extension(IPreferences preferences)
	{
		public AppTheme Theme
		{
			get => preferences.ReadKey(ThemeKey, AppTheme.System);
			set => preferences.SaveKey(ThemeKey, value);
		}

		public AppBackdrop Backdrop
		{
			get => preferences.ReadKey(BackdropKey, AppBackdrop.Mica);
			set => preferences.SaveKey(BackdropKey, value);
		}
	}
}
