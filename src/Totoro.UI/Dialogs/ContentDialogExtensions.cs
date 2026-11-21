using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Totoro.UI.Dialogs;

public static class ContentDialogExtensions
{
	/// <summary>
	/// Shows the dialog in the window's theme, following it while the dialog is open. Dialogs sit in the popup layer,
	/// outside the window content, so they don't inherit the theme set on it.
	/// </summary>
	public static async Task<ContentDialogResult> ShowThemedAsync(this ContentDialog dialog)
	{
		if (dialog.XamlRoot?.Content is not FrameworkElement root)
		{
			return await dialog.ShowAsync();
		}

		void Sync(FrameworkElement sender, object args) => dialog.RequestedTheme = sender.ActualTheme;

		Sync(root, null!);
		root.ActualThemeChanged += Sync;
		try
		{
			return await dialog.ShowAsync();
		}
		finally
		{
			root.ActualThemeChanged -= Sync;
		}
	}
}
