using System.ComponentModel;
using Totoro.Core.Modules.Anime;
using Totoro.Core.Navigation;

namespace Totoro.Core.Modules;

/// <summary>
/// What anime and manga have in common, for what shows either the same way: cards and their overlays, the user's list
/// page and its filters. Changes to the user's entry are raised as a change of "Tracking".
/// </summary>
public interface IMediaModel : INotifyPropertyChanged
{
	long Id { get; }
	MediaMode Kind { get; }
	Guid? ServiceId { get; }
	string Title { get; }
	string EngTitle { get; }
	string RomajiTitle { get; }
	IReadOnlyCollection<string> AlternateTitles { get; }
	string Image { get; }
	/// <summary>Out of 100.</summary>
	float? MeanScore { get; }
	int Popularity { get; }
	IReadOnlyCollection<string> Genres { get; }

	/// <summary>Where it is on the user's list; null when it isn't on it.</summary>
	ListItemStatus? ListStatus { get; }

	/// <summary>The user's score, out of 10.</summary>
	double? UserScore { get; }

	/// <summary>Episodes watched, or chapters read.</summary>
	int? Progress { get; }

	/// <summary>Its episodes, or chapters; null while not known.</summary>
	int? Total { get; }

	DateTime? StartDate { get; }
	DateTime? FinishDate { get; }
	DateTime? UpdatedAt { get; }
}

public static class MediaText
{
	/// <summary>What a list status is called for anime or manga, e.g. "Watching" or "Reading".</summary>
	public static string Status(ListItemStatus? status, MediaMode kind) => (status, kind) switch
	{
		(ListItemStatus.Watching, MediaMode.Manga) => "Reading",
		(ListItemStatus.Watching, _) => "Watching",
		(ListItemStatus.PlanToWatch, MediaMode.Manga) => "Plan to read",
		(ListItemStatus.PlanToWatch, _) => "Plan to watch",
		(ListItemStatus.Completed, _) => "Completed",
		(ListItemStatus.OnHold, _) => "On hold",
		(ListItemStatus.Dropped, _) => "Dropped",
		(ListItemStatus.Rewatching, MediaMode.Manga) => "Rereading",
		(ListItemStatus.Rewatching, _) => "Rewatching",
		_ => "Not on list",
	};
}
