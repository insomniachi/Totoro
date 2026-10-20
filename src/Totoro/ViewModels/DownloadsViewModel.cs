using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules.Streaming;
using Totoro.Playback;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>
/// The anime in the download folder, and the episodes downloaded or downloading (see <see cref="DownloadService"/>); where
/// they go is in Settings.
/// </summary>
public sealed partial class DownloadsViewModel(DownloadService downloads, IDialogService dialogs) : NavigatableViewModel
{
	public ObservableCollection<DownloadItem> Downloads => downloads.Downloads;

	public ObservableCollection<DownloadedAnime> Library => downloads.Library;

	/// <summary>Why editing or deleting an anime failed; empty, never null, when it didn't.</summary>
	[ObservableProperty] public partial string Error { get; set; } = "";

	public void OpenFolder() => downloads.OpenFolder();

	public void Refresh() => Run(downloads.RefreshLibrary);

	public async Task EditInfoAsync(DownloadedAnime anime)
	{
		if (await dialogs.ShowAsync<DownloadInfoDialogViewModel, DownloadInfo>(anime) is { } info)
		{
			Run(() => downloads.SaveInfo(anime.Name, info));
		}
	}

	public void Delete(DownloadedAnime anime) => Run(() => downloads.Delete(anime.Name));

	protected override Task OnNavigatedToInternalAsync()
	{
		Refresh();
		return Task.CompletedTask;
	}

	private void Run(Action action)
	{
		try
		{
			action();
			Error = "";
		}
		catch (Exception ex) when (ex is DownloadException or IOException or UnauthorizedAccessException or OperationCanceledException)
		{
			Error = ex.Message;
		}
	}
}
