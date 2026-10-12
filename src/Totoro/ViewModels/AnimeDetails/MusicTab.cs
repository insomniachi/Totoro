using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Totoro.Core.Services;

namespace Totoro.ViewModels.AnimeDetails;

/// <param name="Header">e.g. "Openings".</param>
public sealed record SongGroup(string Header, IReadOnlyList<SongItem> Songs);

/// <summary>
/// The anime's songs (see <see cref="AnimeSongService"/>): its openings, endings and insert songs, each group under its
/// heading. A song's audio plays in its card; its video in the media player.
/// </summary>
public sealed partial class MusicTab(
	Func<Task<IReadOnlyList<AnimeSong>>> fetch,
	Func<AnimeSong, SongItem> toItem,
	ISongPlayer player,
	ILogger logger) : LazyDetailsTab("No songs listed.", logger)
{
	[ObservableProperty]
	public partial IReadOnlyList<SongGroup> Groups { get; private set; } = [];

	/// <summary>Stops the song playing, e.g. when the tab is left.</summary>
	public void StopAudio()
	{
		if (Groups.SelectMany(x => x.Songs).Any(x => x.IsCurrent))
		{
			player.Stop();
		}
	}

	protected override async Task<int> LoadAsync()
	{
		var songs = await fetch();
		Groups =
		[
			.. songs.GroupBy(x => x.Kind)
					.Select(x => new SongGroup(HeaderOf(x.Key), [.. x.Select(toItem)]))
		];
		return songs.Count;
	}

	private static string HeaderOf(AnimeSongKind kind) => kind switch
	{
		AnimeSongKind.Opening => "Openings",
		AnimeSongKind.Ending => "Endings",
		_ => "Insert songs",
	};
}
