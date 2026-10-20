using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.AnimatedVisuals;

namespace Totoro.UI;

public static class Icons
{
	public static SymbolIcon Home => new(Symbol.Home);
	public static FontIcon HomeFilled => new() { Glyph = "\uEA8A" };
	public static SymbolIcon Settings => new(Symbol.Setting);
	public static SymbolIcon Search => new(Symbol.Find);

	// Animated icons, from WinUI's built-in animations or our own (AnimatedVisuals). Use one wherever there is one
	// (there's no home, for instance). They animate with their container's AnimatedIcon.State, which must be one of the
	// names WinUI's own templates use (Normal, PointerOver, Pressed): other names leave them still. The fallback shows
	// where animations can't play.

	/// <summary>The magnifier moving, as in WinUI's search box.</summary>
	public static AnimatedIcon SearchAnimated => new()
	{
		Source = new AnimatedFindVisualSource(),
		FallbackIconSource = new SymbolIconSource { Symbol = Symbol.Find },
	};

	/// <summary>
	/// Books on a shelf, the leaning one swinging upright as the pointer comes over (our own, from its Lottie file in
	/// AnimatedVisuals; see Generate.ps1 there).
	/// </summary>
	public static AnimatedIcon LibraryAnimated => new()
	{
		Source = new AnimatedVisuals.AnimatedLibraryVisualSource(),
		FallbackIconSource = new FontIconSource { Glyph = "" },
	};

	/// <summary>
	/// A play triangle nudging forward as the pointer comes over, and when clicked punching forward as a ring bursts out
	/// from it (our own, from its Lottie file in AnimatedVisuals).
	/// </summary>
	public static AnimatedIcon PlayAnimated => new()
	{
		Source = new AnimatedVisuals.AnimatedPlayVisualSource(),
		FallbackIconSource = new SymbolIconSource { Symbol = Symbol.Play },
	};

	// The rest animate when clicked, not on hover (our own, from their Lottie files in AnimatedVisuals).

	/// <summary>A download arrow dropping into its tray, which gives under it, as a new arrow slides in.</summary>
	public static AnimatedIcon DownloadAnimated => new()
	{
		Source = new AnimatedVisuals.AnimatedDownloadVisualSource(),
		FallbackIconSource = new FontIconSource { Glyph = "" },
	};

	/// <summary>A plug jolting up into its socket, with sparks flying from its prongs.</summary>
	public static AnimatedIcon PluginsAnimated => new()
	{
		Source = new AnimatedVisuals.AnimatedPluginsVisualSource(),
		FallbackIconSource = new FontIconSource { Glyph = "" },
	};

	/// <summary>A test tube shaking, bubbles rising out of it.</summary>
	public static AnimatedIcon TestAnimated => new()
	{
		Source = new AnimatedVisuals.AnimatedTestVisualSource(),
		FallbackIconSource = new FontIconSource { Glyph = "" },
	};

	/// <summary>The gear turning, as on WinUI's own Settings menu item.</summary>
	public static AnimatedIcon SettingsAnimated => new()
	{
		Source = new AnimatedSettingsVisualSource(),
		FallbackIconSource = new SymbolIconSource { Symbol = Symbol.Setting },
	};

	/// <summary>A lab beaker, for experiments and playgrounds.</summary>
	public static FontIcon Test => new() { Glyph = "\uF196" };

	/// <summary>Marketplace/store icon for community plugins.</summary>
	public static FontIcon Plugins => new() { Glyph = "\uEA86" };

	public static FontIcon Play => new() { Glyph = "\uE768" };
	public static FontIcon Clock => new() { Glyph = "\uE823" };
	public static FontIcon Completed => new() { Glyph = "\uE930" };
	public static FontIcon Pause => new() { Glyph = "\uE769" };
	public static FontIcon Cancel => new() { Glyph = "\uE711" };
	public static FontIcon Download => new() { Glyph = "\uE896" };

	/// <summary>
	/// Resolves an icon key (the name of one of the properties above) coming from UI-agnostic code, such as a filter option.
	/// Returns a new element on every call, since an element can only have one parent.
	/// </summary>
	public static IconElement? FromKey(string? key) => key switch
	{
		nameof(Home) => Home,
		nameof(HomeFilled) => HomeFilled,
		nameof(Settings) => Settings,
		nameof(Test) => Test,
		nameof(Plugins) => Plugins,
		nameof(Search) => Search,
		nameof(Play) => Play,
		nameof(Clock) => Clock,
		nameof(Completed) => Completed,
		nameof(Pause) => Pause,
		nameof(Cancel) => Cancel,
		nameof(Download) => Download,
		_ => null
	};
}
