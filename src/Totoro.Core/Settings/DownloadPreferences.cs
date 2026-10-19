namespace Totoro.Core.Settings;

public static class DownloadPreferences
{
	public const string DownloadFolderKey = "DownloadFolder";
	public const string YtDlpPathKey = "YtDlpPath";

	/// <summary>Where downloaded episodes go unless the user picks a folder: Videos\Totoro.</summary>
	public static string DefaultDownloadFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Totoro");

	extension(IPreferences preferences)
	{
		/// <summary>Where episodes are downloaded to, a folder for each anime (see DownloadService).</summary>
		public string DownloadFolder
		{
			get => preferences.ReadKey(DownloadFolderKey, "") is { Length: > 0 } folder ? folder : DefaultDownloadFolder;
			set => preferences.SaveKey(DownloadFolderKey, value);
		}

		/// <summary>yt-dlp.exe, which downloads HLS streams; empty to find it on the PATH.</summary>
		public string YtDlpPath
		{
			get => preferences.ReadKey(YtDlpPathKey, "") ?? "";
			set => preferences.SaveKey(YtDlpPathKey, value);
		}
	}
}
