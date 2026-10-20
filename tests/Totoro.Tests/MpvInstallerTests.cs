using Totoro.Core.Helpers;
using Totoro.Modules.MediaPlayers.Mpv;
using Xunit;

namespace Totoro.Tests;

public class MpvInstallerTests
{
	private static GitHubAsset Asset(string name) => new(name, new Uri($"https://example.com/{name}"), 1, null);

	[Fact]
	public void PickAssets_TakesThe64BitMpvAndFfmpeg()
	{
		// The files of shinchiro's release 20261020.
		string[] names =
		[
			"ffmpeg-aarch64-git-38ac3a923.7z", "ffmpeg-i686-git-38ac3a923.7z", "ffmpeg-x86_64-git-38ac3a923.7z",
			"ffmpeg-x86_64-v3-git-38ac3a923.7z", "mpv-aarch64-20261020-git-6a262d11f6.7z", "mpv-dev-x86_64-20261020-git-6a262d11f6.7z",
			"mpv-dev-x86_64-v3-20261020-git-6a262d11f6.7z", "mpv-i686-20261020-git-6a262d11f6.7z", "mpv-x86_64-20261020-git-6a262d11f6.7z",
			"mpv-x86_64-v3-20261020-git-6a262d11f6.7z",
		];

		var picked = MpvInstaller.PickAssets(names.Select(Asset));

		Assert.Equal(["mpv-x86_64-20261020-git-6a262d11f6.7z", "ffmpeg-x86_64-git-38ac3a923.7z"], picked!.Select(x => x.Name));
	}

	[Fact]
	public void PickAssets_NullWhenOneIsMissing()
	{
		Assert.Null(MpvInstaller.PickAssets([Asset("mpv-x86_64-v3-20261020-git-6a262d11f6.7z"), Asset("ffmpeg-x86_64-git-38ac3a923.7z")]));
	}
}
