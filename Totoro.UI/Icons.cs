using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI;

public static class Icons
{
	public static SymbolIcon Home => new(Symbol.Home);
	public static FontIcon HomeFilled => new() { Glyph = "\uEA8A" };
}
