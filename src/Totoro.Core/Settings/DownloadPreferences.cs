namespace Totoro.Core.Settings;

public static class DownloadPreferences
{
	public const string DownloadFolderKey = "DownloadFolder";
	public const string YtDlpPathKey = "YtDlpPath";
	public const string PlayDownloadsFirstKey = "PlayDownloadsFirst";
	public const string AutoDownloadNextKey = "AutoDownloadNext";
	public const string NotifyNewEpisodesKey = "NotifyNewEpisodes";
	public const string NotifiedEpisodesKey = "NotifiedEpisodes";

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

		/// <summary>Whether the user's told when the next episode of an anime they're watching is out, if it isn't downloaded (see AutoDownloader).</summary>
		public bool NotifyNewEpisodes
		{
			get => preferences.ReadKey(NotifyNewEpisodesKey, false);
			set => preferences.SaveKey(NotifyNewEpisodesKey, value);
		}

		/// <summary>The last episode the user was told is out, by anime id, so they aren't told again after a restart.</summary>
		public Dictionary<long, int> NotifiedEpisodes
		{
			get => preferences.ReadKey<Dictionary<long, int>>(NotifiedEpisodesKey) ?? [];
			set => preferences.SaveKey(NotifiedEpisodesKey, value);
		}
	}
}
