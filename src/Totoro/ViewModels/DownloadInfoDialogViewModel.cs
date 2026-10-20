using CommunityToolkit.Mvvm.ComponentModel;
using Totoro.Core.Dialogs;
using Totoro.Core.Modules.Streaming;
using Totoro.Playback;

namespace Totoro.ViewModels;

/// <summary>Edits (or adds) a downloaded anime's info. Returns it; saving it is up to the caller.</summary>
public sealed partial class DownloadInfoDialogViewModel : DialogViewModel<DownloadInfo>
{
	public DownloadInfoDialogViewModel(DownloadedAnime anime)
	{
		Title = anime.Info is null ? $"Add info for {anime.Name}" : $"Edit info for {anime.Name}";
		PrimaryButtonText = "Save";
		AnimeTitle = anime.Info?.Title ?? anime.Name;
		OtherTitles = string.Join(Environment.NewLine, anime.Info?.OtherTitles ?? []);
		// NumberBox uses NaN for an empty box.
		AnilistId = anime.Info?.AnilistId ?? double.NaN;
		MalId = anime.Info?.MalId ?? double.NaN;
	}

	[ObservableProperty]
	public partial string AnimeTitle { get; set; }

	/// <summary>One a line.</summary>
	[ObservableProperty]
	public partial string OtherTitles { get; set; }

	[ObservableProperty]
	public partial double AnilistId { get; set; }

	[ObservableProperty]
	public partial double MalId { get; set; }

	partial void OnAnimeTitleChanged(string value) => CanConfirm = !string.IsNullOrWhiteSpace(value);

	public override Task<bool> ConfirmAsync()
	{
		Result = new DownloadInfo(AnimeTitle, OtherTitles.Split('\r', '\n'), Id(AnilistId), Id(MalId));
		return Task.FromResult(true);
	}

	private static long? Id(double value) => double.IsNaN(value) || value <= 0 ? null : (long)value;
}
