using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI;

public static class Icons
{
	public static SymbolIcon Home => new(Symbol.Home);
	public static FontIcon HomeFilled => new() { Glyph = "\uEA8A" };
	public static SymbolIcon Settings => new(Symbol.Setting);
	public static SymbolIcon Search => new(Symbol.Find);

	public static FontIcon Play => new() { Glyph = "\uE768" };
	public static FontIcon Clock => new() { Glyph = "\uE823" };
	public static FontIcon Completed => new() { Glyph = "\uE930" };
	public static FontIcon Pause => new() { Glyph = "\uE769" };
	public static FontIcon Cancel => new() { Glyph = "\uE711" };

	/// <summary>
	/// Resolves an icon key (the name of one of the properties above) coming from UI-agnostic code, such as a filter option.
	/// Returns a new element on every call, since an element can only have one parent.
	/// </summary>
	public static IconElement? FromKey(string? key) => key switch
	{
		nameof(Home) => Home,
		nameof(HomeFilled) => HomeFilled,
		nameof(Settings) => Settings,
		nameof(Search) => Search,
		nameof(Play) => Play,
		nameof(Clock) => Clock,
		nameof(Completed) => Completed,
		nameof(Pause) => Pause,
		nameof(Cancel) => Cancel,
		_ => null
	};
}
