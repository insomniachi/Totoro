using Totoro.Core.Modules.Streaming;
using Totoro.Playback;
using Xunit;

namespace Totoro.Tests;

public class DownloadTests
{
	[Fact]
	public void Names_RoundTrip()
	{
		var folder = DownloadNames.Folder("Re:Zero [Director's Cut]?", 21355, 31240);
		Assert.Equal("Re Zero Director's Cut [anilist-21355] [mal-31240]", folder);
		Assert.Equal((21355L, 31240L), DownloadNames.ParseIds(folder));
		Assert.Equal((null, 52991L), DownloadNames.ParseIds(DownloadNames.Folder("Frieren", null, 52991)));

		Assert.Equal(5, DownloadNames.ParseEpisode(DownloadNames.Episode("Frieren", 5) + ".mkv"));
		Assert.Equal(112, DownloadNames.ParseEpisode("One Piece - 112.mp4"));
	}

	[Fact]
	public void Describe_ShowsWhatsKnown()
	{
		var mb = 1 << 20;
		Assert.Null(DownloadService.Size(null));
		Assert.Equal("640 KB", DownloadService.Size(640 * 1024));
		Assert.Equal($"{1.5:0.0} GB", DownloadService.Size(1.5 * (1L << 30)));
		Assert.Equal($"{0.25:P0} · {120.0:0.0} MB of {480.0:0.0} MB · {6.0:0.0} MB/s",
					 DownloadService.Describe(new DownloadProgress(0.25, 6.0 * mb, 120.0 * mb, 480.0 * mb)));
		Assert.Equal($"{3.0:0.0} MB", DownloadService.Describe(new DownloadProgress(-1, null, 3.0 * mb)));
		Assert.Equal("Downloading…", DownloadService.Describe(new DownloadProgress(-1, null)));
	}
}
