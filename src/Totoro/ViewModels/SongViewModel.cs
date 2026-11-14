using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Commands;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Services;
using Totoro.UI.Navigation;

namespace Totoro.ViewModels;

/// <summary>
/// A song picked in the title bar's search, in the side pane: its card (audio with the spectrum and lyrics, or the video
/// in the media player), and the anime it's from, which opens in the selected database.
/// </summary>
public sealed partial class SongViewModel(SongMatch match, AnimeSongService songs, ISongPlayer player, AnimeCommands commands) : NavigatableViewModel
{
	public SongItem Song { get; } = new(match.Anime, match.Song, songs, player);

	public AnimeModel Anime => match.Anime;

	/// <summary>e.g. "OP2 of Oshi no Ko".</summary>
	public string Heading => $"{match.Song.Label} of {match.Anime.Title}";

	/// <summary>Why the anime couldn't be opened; "" when it could.</summary>
	[ObservableProperty] public partial string Message { get; private set; } = "";

	/// <summary>Opens the anime's details, from the selected database.</summary>
	[RelayCommand]
	private async Task OpenAnimeAsync()
	{
		// The song search's anime has only its ids in each database: it can't be shown as it is.
		var anime = await commands.InDatabaseAsync(match.Anime);
		if (ReferenceEquals(anime, match.Anime))
		{
			Message = "Couldn't find this anime in your anime database.";
			return;
		}

		Message = "";
		await commands.ShowDetailsCommand.ExecuteAsync(anime);
	}
}
