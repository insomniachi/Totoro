using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Settings;
using Totoro.Playback;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>The episodes downloaded or downloading (see <see cref="DownloadService"/>), and the folder they go to.</summary>
public sealed partial class DownloadsViewModel(DownloadService downloads, IPreferences preferences) : NavigatableViewModel
{
	public ObservableCollection<DownloadItem> Downloads => downloads.Downloads;

	[ObservableProperty] public partial string Folder { get; private set; } = preferences.DownloadFolder;

	/// <summary>Downloads go to <paramref name="folder"/> from now on.</summary>
	public void SetFolder(string folder) => Folder = preferences.DownloadFolder = folder;

	/// <summary>yt-dlp.exe, which downloads HLS streams; empty, it's found on the PATH.</summary>
	[ObservableProperty] public partial string YtDlpPath { get; set; } = preferences.YtDlpPath;

	partial void OnYtDlpPathChanged(string value) => preferences.YtDlpPath = value.Trim().Trim('"');

	// An agent may have changed them.
	protected override Task OnNavigatedToInternalAsync()
	{
		Folder = preferences.DownloadFolder;
		YtDlpPath = preferences.YtDlpPath;
		return Task.CompletedTask;
	}

	[RelayCommand]
	private void OpenFolder()
	{
		Directory.CreateDirectory(Folder);
		Process.Start("explorer.exe", $"\"{Folder}\"");
	}
}
