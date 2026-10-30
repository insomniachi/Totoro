using Totoro.Core.Settings;

namespace Totoro.Core.Navigation;

/// <summary>What the app is showing: the main menu's pages are for one or the other (or both, like Settings).</summary>
public enum MediaMode
{
	Anime,
	Manga,
}

public static class MediaModePreferences
{
	private const string Key = "StartupMode";

	extension(IPreferences preferences)
	{
		/// <summary>The mode the app starts in (a setting; switching mode doesn't change it).</summary>
		public MediaMode StartupMode
		{
			get => preferences.ReadKey(Key, MediaMode.Anime);
			set => preferences.SaveKey(Key, value);
		}
	}
}
