namespace Totoro.Core.Settings;

public static class DownloadPreferences
{
	public const string DownloadFolderKey = "DownloadFolder";
	public const string YtDlpPathKey = "YtDlpPath";
	public const string PlayDownloadsFirstKey = "PlayDownloadsFirst";
	public const string AutoDownloadNextKey = "AutoDownloadNext";

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

		/// <summary>Whether an episode in the download folder is played from there, whatever the anime's provider.</summary>
		public bool PlayDownloadsFirst
		{
			get => preferences.ReadKey(PlayDownloadsFirstKey, true);
			set => preferences.SaveKey(PlayDownloadsFirstKey, value);
		}

		/// <summary>Whether the next episode of each anime the user is watching is downloaded once it's out (see AutoDownloader).</summary>
		public bool AutoDownloadNext
		{
			get => preferences.ReadKey(AutoDownloadNextKey, false);
			set => preferences.SaveKey(AutoDownloadNextKey, value);
		}
	}
}
