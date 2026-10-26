using System.ComponentModel;

namespace Totoro.Core.Modules.Streaming;

// Settings store enums as numbers, so keep the order: changing it changes users' saved choices.
public enum VideoQuality
{
	[Description("1080p")] FullHd,
	[Description("720p")] Hd,
	[Description("480p")] Sd,
}

public enum AudioType
{
	[Description("Sub (original audio, with subtitles)")] Sub,
	[Description("Dub (English dubbed)")] Dub,
}

public static class VideoQualityExtensions
{
	public static int ToResolution(this VideoQuality quality) => quality switch
	{
		VideoQuality.Hd => 720,
		VideoQuality.Sd => 480,
		_ => 1080,
	};
}
