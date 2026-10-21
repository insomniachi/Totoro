using Totoro.UI.Input;
using Windows.System;
using Xunit;

namespace Totoro.Tests;

public class KeyboardShortcutsTests
{
	[Theory]
	[InlineData("Space, K", VirtualKey.K, VirtualKeyModifiers.None, true)]
	[InlineData("Space, K", VirtualKey.K, VirtualKeyModifiers.Control, false)]
	[InlineData("Ctrl+Right", VirtualKey.Right, VirtualKeyModifiers.Control, true)]
	[InlineData("Ctrl+Right", VirtualKey.Right, VirtualKeyModifiers.None, false)]
	[InlineData("shift + ctrl + n", VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, true)]
	[InlineData("Ctrl+Shift+N", VirtualKey.N, VirtualKeyModifiers.Control, false)]
	[InlineData("Hyper+N", VirtualKey.N, VirtualKeyModifiers.None, false)]
	[InlineData("1", VirtualKey.Number1, VirtualKeyModifiers.None, false)]
	[InlineData("Number1", VirtualKey.Number1, VirtualKeyModifiers.None, true)]
	[InlineData("", VirtualKey.K, VirtualKeyModifiers.None, false)]
	public void MatchesKeyWithExactlyItsModifiers(string shortcuts, VirtualKey key, VirtualKeyModifiers held, bool expected) =>
		Assert.Equal(expected, KeyboardShortcuts.IsMatch(shortcuts, key, held));

	[Theory]
	[InlineData("Ctrl+Right", "Ctrl+→")]
	[InlineData("Number1", "1")]
	[InlineData("PageUp", "PageUp")]
	public void LabelsShowArrowsAndDigits(string shortcut, string expected) => Assert.Equal(expected, KeyboardShortcuts.Label(shortcut));
}
