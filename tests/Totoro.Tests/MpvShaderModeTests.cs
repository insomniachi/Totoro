using Totoro.ViewModels.Player;
using Xunit;

namespace Totoro.Tests;

public class MpvShaderModeTests
{
	[Fact]
	public void ParseShaderModes_ReadsAnime4KTemplateLines()
	{
		// As Anime4K's Windows template binds them, with a commented-out line, a clear and a line of the user's own.
		var conf = """
			# Optimized shaders for higher-end GPU
			CTRL+1 no-osd change-list glsl-shaders set "~~/shaders/A.glsl;~~/shaders/B.glsl"; show-text "Anime4K: Mode A (HQ)"
			CTRL+2 no-osd change-list glsl-shaders set "~~/shaders/A.glsl;~~/shaders/C.glsl"; show-text "Anime4K: Mode B (HQ)"
			#CTRL+3 no-osd change-list glsl-shaders set "~~/shaders/D.glsl"; show-text "Anime4K: Mode C (HQ)"
			CTRL+0 no-osd change-list glsl-shaders clr ""; show-text "GLSL shaders cleared"
			g cycle-values glsl-shaders "~~/shaders/nnedi3.hook" ""
			""";

		var modes = MpvPlayerViewModel.ParseShaderModes(conf);

		Assert.Equal(["From 1080p", "From 720p"], modes.Select(x => x.Name));
		Assert.Equal("~~/shaders/A.glsl;~~/shaders/B.glsl", modes[0].Shaders);
		Assert.Equal("Anime4K: Mode A (HQ)", modes[0].Label);
	}

	[Theory]
	[InlineData("Anime4K: Mode C (HQ)", "From 480p / DVD")]
	[InlineData("Anime4K: Mode A+A (Fast)", "From 1080p, stronger")]
	[InlineData("Anime4K: Mode C+A (HQ)", "From 480p / DVD, stronger")]
	[InlineData("My shaders", "My shaders")]
	public void ShaderModeName_SaysWhatVideoItsFor(string label, string name)
	{
		Assert.Equal(name, new MpvPlayerViewModel.ShaderMode(label, "x.glsl").Name);
		Assert.Equal(label.StartsWith("Anime4K"), new MpvPlayerViewModel.ShaderMode(label, "x.glsl").Description is not null);
	}

	[Fact]
	public void ParseShaderModes_NoneWithoutAnime4K()
	{
		Assert.Empty(MpvPlayerViewModel.ParseShaderModes("SPACE cycle pause\n"));
	}
}
