using System.Collections.ObjectModel;
using Totoro.Playback;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>The episodes downloaded or downloading (see <see cref="DownloadService"/>); where they go is in Settings.</summary>
public sealed partial class DownloadsViewModel(DownloadService downloads) : NavigatableViewModel
{
	public ObservableCollection<DownloadItem> Downloads => downloads.Downloads;

	public void OpenFolder() => downloads.OpenFolder();
}
