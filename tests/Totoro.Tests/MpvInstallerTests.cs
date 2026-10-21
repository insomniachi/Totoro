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

	[Fact]
	public void Anime4K_SwapsAndRemovesOnlyWhatWasAdded()
	{
		var own = "volume=50" + Environment.NewLine;

		var low = MpvInstaller.WithAnime4K(own, Anime4KGpu.LowerEnd, "low");
		Assert.True(MpvInstaller.HasAnime4K(low, Anime4KGpu.LowerEnd));
		Assert.False(MpvInstaller.HasAnime4K(low, Anime4KGpu.HigherEnd));

		var high = MpvInstaller.WithAnime4K(low, Anime4KGpu.HigherEnd, "high");
		Assert.DoesNotContain("low", high);
		Assert.True(MpvInstaller.HasAnime4K(high, Anime4KGpu.HigherEnd));

		Assert.Equal(own, MpvInstaller.WithoutAddedAnime4K(high));
	}

	[Fact]
	public void Anime4K_LeavesTheUsersOwnAlone()
	{
		var own = "glsl-shaders=\"~~/shaders/Anime4K_Clamp_Highlights.glsl\"";
		Assert.True(MpvInstaller.HasAnime4K(own, Anime4KGpu.HigherEnd));
		Assert.False(MpvInstaller.HasAnime4K("", Anime4KGpu.LowerEnd));
		Assert.Equal(own, MpvInstaller.WithoutAddedAnime4K(own));
	}
}
