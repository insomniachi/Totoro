namespace Totoro.Core.Modules.Media;

/// <summary>How video is scaled or stretched to the player's viewport.</summary>
public enum PlayerAspectRatio
{
	/// <summary>Original aspect ratio (preserving black bars if needed).</summary>
	Fit,

	/// <summary>Stretch to fill the player window completely (no black bars).</summary>
	Stretch,

	/// <summary>16:9 widescreen ratio.</summary>
	Ratio16_9,

	/// <summary>4:3 standard ratio.</summary>
	Ratio4_3,

	/// <summary>21:9 ultrawide ratio.</summary>
	Ratio21_9,
}
