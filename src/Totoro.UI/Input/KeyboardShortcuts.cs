using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace Totoro.UI.Input;

/// <summary>
/// Keyboard shortcuts kept as text: alternatives separated by commas, each a key's name (VirtualKey's) after the
/// modifiers held with it, e.g. "Space, Ctrl+Right".
/// </summary>
public static class KeyboardShortcuts
{
	// In the order they're written in.
	private static readonly (VirtualKeyModifiers Modifier, string Name, VirtualKey[] Keys)[] s_modifiers =
	[
		(VirtualKeyModifiers.Control, "Ctrl", [VirtualKey.Control]),
		(VirtualKeyModifiers.Shift, "Shift", [VirtualKey.Shift]),
		(VirtualKeyModifiers.Menu, "Alt", [VirtualKey.Menu]),
		(VirtualKeyModifiers.Windows, "Win", [VirtualKey.LeftWindows, VirtualKey.RightWindows]),
	];

	/// <summary>The shortcuts, e.g. ["Space", "Ctrl+Right"].</summary>
	public static string[] Split(string shortcuts) => shortcuts.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

	/// <summary>A shortcut's keys, modifiers first, e.g. ["Ctrl", "Right"].</summary>
	public static string[] GetKeys(string shortcut) => shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

	/// <summary>A key with the modifiers held now, e.g. "Ctrl+Shift+K".</summary>
	public static string Format(VirtualKey key) =>
		string.Join("+", s_modifiers.Where(x => GetModifiers().HasFlag(x.Modifier)).Select(x => x.Name).Append(key.ToString()));

	/// <summary>Whether it's a modifier, which makes a shortcut only with another key.</summary>
	public static bool IsModifier(VirtualKey key) =>
		key is VirtualKey.LeftShift or VirtualKey.RightShift or VirtualKey.LeftControl or VirtualKey.RightControl or
			VirtualKey.LeftMenu or VirtualKey.RightMenu || s_modifiers.Any(x => x.Keys.Contains(key));

	/// <summary>Whether a key pressed now, with the modifiers held, is one of the shortcuts (modifiers in any order or case).</summary>
	public static bool IsPressed(string shortcuts, VirtualKey key) => IsMatch(shortcuts, key, GetModifiers());

	/// <summary>Whether a key with these modifiers is one of the shortcuts (modifiers in any order or case).</summary>
	public static bool IsMatch(string shortcuts, VirtualKey key, VirtualKeyModifiers held)
	{
		return Split(shortcuts).Any(shortcut =>
		{
			var keys = GetKeys(shortcut);
			return keys.Length > 0 && string.Equals(keys[^1], key.ToString(), StringComparison.OrdinalIgnoreCase) &&
				   ParseModifiers(keys[..^1]) == held;
		});
	}

	/// <summary>How a key shows on a keycap: arrows and digits as themselves, e.g. "←" for Left, "1" for Number1.</summary>
	public static string KeyLabel(string key) => key switch
	{
		"Left" => "←",
		"Right" => "→",
		"Up" => "↑",
		"Down" => "↓",
		_ when key.StartsWith("Number") && key.Length == 7 => key[6..],
		_ when key.StartsWith("NumberPad") => $"Num {key[9..]}",
		_ => key,
	};

	/// <summary>A shortcut as text to show, e.g. "Ctrl+→".</summary>
	public static string Label(string shortcut) => string.Join("+", GetKeys(shortcut).Select(KeyLabel));

	private static VirtualKeyModifiers GetModifiers() => s_modifiers
		.Where(x => x.Keys.Any(key => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down)))
		.Aggregate(VirtualKeyModifiers.None, (all, x) => all | x.Modifier);

	// Null for a name that isn't a modifier, so the shortcut matches nothing.
	private static VirtualKeyModifiers? ParseModifiers(IEnumerable<string> names)
	{
		var modifiers = VirtualKeyModifiers.None;
		foreach (var name in names)
		{
			var modifier = s_modifiers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
			if (modifier.Name is null)
			{
				return null;
			}

			modifiers |= modifier.Modifier;
		}

		return modifiers;
	}
}
