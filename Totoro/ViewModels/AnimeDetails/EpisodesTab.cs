using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Module.Anime;

namespace Totoro.ViewModels.AnimeDetails;

// A display record rather than the model: the XAML compiler can't generate binding code for init-only properties.
public sealed record EpisodeItem(string Number, string Title, string Details, string? Image, string Overview, bool IsWatched);

/// <summary>The anime's episodes, with the ones already watched marked.</summary>
public sealed partial class EpisodesTab(Func<Task<List<EpisodeInfo>>> fetch, Func<int> watchedEpisodes, ILogger logger)
	: LazyDetailsTab("No episode information yet.", logger)
{
	private List<EpisodeInfo> _episodes = [];

	[ObservableProperty]
	public partial IReadOnlyList<EpisodeItem> Episodes { get; private set; } = [];

	protected override async Task<int> LoadAsync()
	{
		_episodes = await fetch();
		UpdateWatched();
		return _episodes.Count;
	}

	/// <summary>Re-marks watched episodes, e.g. after the tracking was edited.</summary>
	public void UpdateWatched()
	{
		var watched = watchedEpisodes();
		Episodes = [.. _episodes.Select(x => ToItem(x, watched))];
	}

	private static EpisodeItem ToItem(EpisodeInfo episode, int watched)
	{
		var number = episode.IsSpecial ? $"S{episode.EpisodeNumber}" : episode.EpisodeNumber.ToString();
		var title = NullIfEmpty(episode.Titles.English) ?? NullIfEmpty(episode.Titles.Romaji) ?? NullIfEmpty(episode.Titles.Japanese)
					?? (episode.IsSpecial ? "Special" : $"Episode {episode.EpisodeNumber}");

		List<string> details = [];
		if (episode.AirDateUtc is { } aired)
		{
			details.Add(aired.ToLocalTime().ToString("d MMM yyyy"));
		}

		if (episode.Runtime > 0)
		{
			details.Add($"{episode.Runtime} min");
		}

		return new EpisodeItem(number, title, string.Join(" · ", details), NullIfEmpty(episode.Image), episode.Overview,
							   IsWatched: !episode.IsSpecial && episode.EpisodeNumber <= watched);
	}
}
