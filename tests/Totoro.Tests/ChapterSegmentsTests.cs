using Totoro.Core.Modules.Media;
using Totoro.Playback;
using Xunit;

namespace Totoro.Tests;

public class ChapterSegmentsTests
{
	private static readonly TimeSpan s_duration = TimeSpan.FromMinutes(24);

	private static MediaChapter At(string title, double seconds) => new(title, TimeSpan.FromSeconds(seconds));

	[Fact]
	public void FromChapters_ByTitle()
	{
		var segments = SegmentSkipper.FromChapters([At("Prologue", 0), At("Opening", 120), At("Part A", 210), At("ED", 1290), At("Preview", 1380)], s_duration);

		Assert.Equal(
			[
				new MediaSegment(MediaSegmentType.Opening, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(210)),
				new MediaSegment(MediaSegmentType.Ending, TimeSpan.FromSeconds(1290), TimeSpan.FromSeconds(1380)),
			],
			segments);
	}

	[Fact]
	public void FromChapters_UntitledByLength()
	{
		var segments = SegmentSkipper.FromChapters([At("Chapter 1", 0), At("Chapter 2", 100), At("Chapter 3", 190), At("Chapter 4", 1300), At("Chapter 5", 1390)], s_duration);

		Assert.Equal(
			[
				new MediaSegment(MediaSegmentType.Opening, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(100)),
				new MediaSegment(MediaSegmentType.Ending, TimeSpan.FromSeconds(1300), TimeSpan.FromSeconds(1390)),
			],
			segments);
	}

	[Fact]
	public void FromChapters_NamedContentOfSongLength_IsNotGuessed()
	{
		Assert.Empty(SegmentSkipper.FromChapters([At("Part A", 0), At("Eyecatch", 600), At("Part B", 1300), At("Part C", 1390)], s_duration));
	}
}
