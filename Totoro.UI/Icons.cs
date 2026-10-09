using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI;

public static class Icons
{
	public static SymbolIcon Home => new(Symbol.Home);
	public static FontIcon HomeFilled => new() { Glyph = "" };

	public static FontIcon Play => new() { Glyph = "" };
	public static FontIcon Clock => new() { Glyph = "" };
	public static FontIcon Completed => new() { Glyph = "" };
	public static FontIcon Pause => new() { Glyph = "" };
	public static FontIcon Cancel => new() { Glyph = "" };

	/// <summary>
	/// Resolves an icon key (the name of one of the properties above) coming from UI-agnostic code, such as a filter option.
	/// Returns a new element on every call, since an element can only have one parent.
	/// </summary>
	public static IconElement? FromKey(string? key) => key switch
	{
		nameof(Home) => Home,
		nameof(HomeFilled) => HomeFilled,
		nameof(Play) => Play,
		nameof(Clock) => Clock,
		nameof(Completed) => Completed,
		nameof(Pause) => Pause,
		nameof(Cancel) => Cancel,
		_ => null
	};
}
