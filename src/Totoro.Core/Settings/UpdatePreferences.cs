namespace Totoro.Core.Settings;

public static class UpdatePreferences
{
	public const string CheckForUpdatesKey = "CheckForUpdates";

	extension(IPreferences preferences)
	{
		/// <summary>Whether Totoro looks for a newer version when it starts (and offers it); see UpdateService.</summary>
		public bool CheckForUpdates
		{
			get => preferences.ReadKey(CheckForUpdatesKey, true);
			set => preferences.SaveKey(CheckForUpdatesKey, value);
		}
	}
}
