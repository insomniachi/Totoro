using Totoro.Modules.AnimeDatabase.Anilist;
using Xunit;

namespace Totoro.Tests;

public class AnilistTagSplitTests
{
	private static readonly HashSet<string> s_tags = ["Isekai", "Time Skip"];

	[Fact]
	public void SplitGenresAndTags_RoutesEachNameToItsHalf()
	{
		var (genres, tags) = AnilistMetadataProvider.SplitGenresAndTags(["Comedy", "Isekai", "Romance", "Time Skip"], s_tags);
		Assert.Equal(["Comedy", "Romance"], genres);
		Assert.Equal(["Isekai", "Time Skip"], tags);

		(genres, tags) = AnilistMetadataProvider.SplitGenresAndTags(["Comedy"], s_tags);
		Assert.Equal(["Comedy"], genres);
		Assert.Null(tags);

		(genres, tags) = AnilistMetadataProvider.SplitGenresAndTags(["Isekai"], s_tags);
		Assert.Null(genres);
		Assert.Equal(["Isekai"], tags);

		Assert.Equal((null, null), AnilistMetadataProvider.SplitGenresAndTags(null, s_tags));
		Assert.Equal((null, null), AnilistMetadataProvider.SplitGenresAndTags([], s_tags));
	}
}
