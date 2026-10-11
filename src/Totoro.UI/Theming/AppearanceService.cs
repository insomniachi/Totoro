using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Totoro.Core.Settings;

namespace Totoro.UI.Theming;

/// <summary>Applies the theme and backdrop saved in <see cref="IPreferences"/> to the main window.</summary>
public sealed class AppearanceService(IPreferences preferences, Func<Window?> window) : IAppearanceService
{
	public AppTheme Theme
	{
		get => preferences.Theme;
		set
		{
			preferences.Theme = value;
			Apply();
		}
	}

	public AppBackdrop Backdrop
	{
		get => preferences.Backdrop;
		set
		{
			preferences.Backdrop = value;
			Apply();
		}
	}

	public void Apply()
	{
		if (window() is not { Content: FrameworkElement root } w)
		{
			return;
		}

		var theme = preferences.Theme;
		root.RequestedTheme = theme switch
		{
			AppTheme.Light => ElementTheme.Light,
			AppTheme.Dark => ElementTheme.Dark,
			_ => ElementTheme.Default,
		};

		// The caption buttons (minimize, maximize, close) don't follow RequestedTheme on their own.
		w.AppWindow.TitleBar.PreferredTheme = theme switch
		{
			AppTheme.Light => TitleBarTheme.Light,
			AppTheme.Dark => TitleBarTheme.Dark,
			_ => TitleBarTheme.UseDefaultAppMode,
		};

		var backdrop = preferences.Backdrop;
		w.SystemBackdrop = backdrop switch
		{
			AppBackdrop.Mica => new MicaBackdrop { Kind = MicaKind.Base },
			AppBackdrop.MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
			AppBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
			_ => null,
		};

		ApplySolidBackground(root);

		if (!_followingThemeChanges)
		{
			// The solid background depends on the effective theme, which also changes with Windows' setting.
			root.ActualThemeChanged += (sender, _) => ApplySolidBackground(sender);
			_followingThemeChanges = true;
		}
	}

	private bool _followingThemeChanges;

	/// <summary>
	/// Without a backdrop the (transparent) content would show the window behind it as black, so paint the base
	/// background. Resource lookups from code resolve against the system theme rather than the one picked here, so
	/// the colour is chosen from the root's actual theme (these are SolidBackgroundFillColorBase's values).
	/// </summary>
	private void ApplySolidBackground(FrameworkElement root)
	{
		if (root is not Panel panel)
		{
			return;
		}

		panel.Background = preferences.Backdrop != AppBackdrop.None
			? null
			: new SolidColorBrush(root.ActualTheme == ElementTheme.Light
				? Windows.UI.Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3)
				: Windows.UI.Color.FromArgb(0xFF, 0x20, 0x20, 0x20));
	}
}
