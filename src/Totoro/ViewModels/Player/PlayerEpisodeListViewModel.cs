using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Modules.Media;

namespace Totoro.ViewModels.Player;

/// <summary>The built-in player's episode list, and moving to the previous or next episode.</summary>
public sealed partial class PlayerEpisodeListViewModel : ObservableObject
{
	/// <summary>Raised when the user picks an episode to play, by its number: from the list, or previous or next.</summary>
	public event EventHandler<int>? EpisodeRequested;

	/// <summary>The anime's episodes, for the episode list.</summary>
	[ObservableProperty] public partial IReadOnlyList<EpisodeListItem> Episodes { get; private set; } = [];

	[ObservableProperty] public partial bool IsEpisodeListOpen { get; set; }

	[ObservableProperty] public partial bool HasPreviousEpisode { get; private set; }

	[ObservableProperty] public partial bool HasNextEpisode { get; private set; }

	/// <inheritdoc cref="IInteractiveMediaPlayer.SetEpisodes"/>
	public void SetEpisodes(IReadOnlyList<PlayerEpisode> episodes)
	{
		Episodes = [.. episodes.Select(x => new EpisodeListItem(x))];
		var current = episodes.FirstOrDefault(x => x.IsCurrent)?.Number;
		HasPreviousEpisode = episodes.Any(x => x.Number == current - 1);
		HasNextEpisode = episodes.Any(x => x.Number == current + 1);
	}

	[RelayCommand]
	private void ToggleEpisodeList() => IsEpisodeListOpen = !IsEpisodeListOpen;

	[RelayCommand]
	private void PlayEpisode(EpisodeListItem? episode)
	{
		IsEpisodeListOpen = false;
		if (episode is { IsCurrent: false })
		{
			EpisodeRequested?.Invoke(this, episode.Number);
		}
	}

	[RelayCommand]
	private void PreviousEpisode() => RequestRelative(-1);

	/// <summary>Asks for the episode <paramref name="offset"/> from the one playing, if the list has it.</summary>
	public void RequestRelative(int offset)
	{
		if (Episodes.FirstOrDefault(x => x.IsCurrent) is { } current && Episodes.Any(x => x.Number == current.Number + offset))
		{
			EpisodeRequested?.Invoke(this, current.Number + offset);
		}
	}
}
