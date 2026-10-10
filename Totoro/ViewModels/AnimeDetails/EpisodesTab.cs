using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Module.Anime;
using Totoro.Core.Module.Media;
using Totoro.Playback;

namespace Totoro.ViewModels.AnimeDetails;

// A display record rather than the model: the XAML compiler can't generate binding code for init-only properties.
/// <param name="Play">What the play button plays; null for specials, which can't be played from here.</param>
/// <param name="ResumeText">Where it'll resume from, e.g. "Resume from 12:34"; empty when there's nothing to resume.</param>
/// <param name="Progress">How far through it the user stopped, from 0 to 1; 0 when there's nothing to resume.</param>
public sealed record EpisodeItem(
	string Number,
	string Title,
	string Details,
	string ResumeText,
	string? Image,
	string Overview,
	bool IsWatched,
	EpisodeToWatch? Play,
	double Progress)
{
	// For x:Bind, which doesn't call a function whose argument is null.
	public bool CanPlay => Play is not null;
	public bool HasProgress => Progress > 0;

	/// <summary>Between the details and the resume text, when there are both.</summary>
	public string ResumeSeparator => Details.Length > 0 && ResumeText.Length > 0 ? " · " : "";

	/// <summary>The play button's accessible name.</summary>
	public string PlayName => HasProgress ? $"Resume episode {Number}" : $"Play episode {Number}";
}

/// <summary>The anime's episodes, with the ones already watched marked and unfinished ones' progress.</summary>
public sealed partial class EpisodesTab(
	Func<Task<List<EpisodeInfo>>> fetch,
	AnimeModel anime,
	IPlaybackProgressStore progress,
	ILogger logger)
	: LazyDetailsTab("No episode information yet.", logger)
{
	private List<EpisodeInfo> _episodes = [];

	[ObservableProperty]
	public partial IReadOnlyList<EpisodeItem> Episodes { get; private set; } = [];

	protected override async Task<int> LoadAsync()
	{
		_episodes = await fetch();
		Refresh();
		return _episodes.Count;
	}

	/// <summary>Re-marks watched episodes and progress, e.g. after watching one or editing the tracking.</summary>
	public void Refresh()
	{
		var watched = anime.Tracking?.WatchedEpisodes ?? 0;
		var saved = progress.GetAll(anime.ProgressKey);
		Episodes = [.. _episodes.Select(x => ToItem(x, watched, saved))];
	}

	private EpisodeItem ToItem(EpisodeInfo episode, int watched, IReadOnlyDictionary<int, EpisodeProgress> saved)
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

		// Shown at the end of the details line (in the accent colour) rather than on a line of its own, so every
		// episode is the same height.
		var resume = episode.IsSpecial ? null : saved.GetValueOrDefault(episode.EpisodeNumber);
		var resumeText = resume is null ? "" : $"Resume from {resume.Position:m\\:ss}";

		return new EpisodeItem(number, title, string.Join(" · ", details), resumeText, NullIfEmpty(episode.Image), episode.Overview,
							   IsWatched: !episode.IsSpecial && episode.EpisodeNumber <= watched,
							   Play: episode.IsSpecial ? null : new EpisodeToWatch(anime, episode.EpisodeNumber),
							   Progress: resume?.Fraction ?? 0);
	}
}
