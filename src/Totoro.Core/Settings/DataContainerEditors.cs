using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Totoro.Core.Settings;

/// <summary>
/// Names of the editors a <see cref="DataContainerProperty"/> can ask for (its EditorType). Set one on a settings
/// property with <c>[Editor(DataContainerEditors.FileBrowser, "")]</c>, or with the property builder's WithEditorType.
/// Most are picked from the property's type; the ones marked "opt in" only by asking for them. A plugin can add its own:
/// any other name, with an IPropertyEditor (Totoro.UI) registered for it.
/// </summary>
public static class DataContainerEditors
{
	/// <summary>Text; also any type without an editor of its own, read back with the type's TypeConverter.</summary>
	public const string TextBox = "TextBox";

	/// <summary>Opt in, for a string: text over several lines.</summary>
	public const string MultilineText = "MultilineText";

	public const string ToggleSwitch = "ToggleSwitch";
	public const string NumberBox = "NumberBox";

	/// <summary>Opt in, for a number with a [Range]: a slider between its ends (a number box without one).</summary>
	public const string Slider = "Slider";

	public const string ComboBox = "ComboBox";

	/// <summary>A [Flags] enum: a check box for each of its values.</summary>
	public const string Flags = "Flags";

	/// <summary>Opt in, for a string: a file's path, typed or picked.</summary>
	public const string FileBrowser = "FileBrowser";

	/// <summary>Opt in, for a string: a folder's path, typed or picked.</summary>
	public const string FolderBrowser = "FolderBrowser";

	/// <summary>A DateOnly, DateTime (its date) or DateTimeOffset: a calendar.</summary>
	public const string DatePicker = "DatePicker";

	/// <summary>A TimeOnly: a time of day.</summary>
	public const string TimePicker = "TimePicker";

	/// <summary>A TimeSpan: typed as h:mm:ss (or d.hh:mm:ss).</summary>
	public const string Duration = "Duration";

	/// <summary>A System.Drawing.Color: a swatch opening a color picker; saved as "#AARRGGBB".</summary>
	public const string Color = "Color";

	/// <summary>A list of strings (string[], List&lt;string&gt;...): one removable chip each, and a box to add one.</summary>
	public const string Tokens = "Tokens";

	/// <summary>A secret, e.g. an API key: shown as dots, and never shown to the assistant.</summary>
	public const string Password = "Password";

	/// <summary>Keyboard shortcuts, as keycaps: a string of key names (VirtualKey's) separated by commas, e.g. "Space, K".</summary>
	public const string Shortcut = "Shortcut";

	/// <summary>A button for a <see cref="SettingsAction"/> value: something to do, not a value to save.</summary>
	public const string Button = "Button";
}

/// <summary>
/// An action in a settings page, e.g. downloading what a module needs: a button running <see cref="Command"/>. What's
/// shown follows changes while it's on screen, so the action can say how it went.
/// </summary>
public sealed partial class SettingsAction(ICommand command) : ObservableObject
{
	public ICommand Command { get; } = command;

	[ObservableProperty] public partial string Label { get; set; } = "";

	/// <summary>The card's description, e.g. the version installed or why it failed; empty for the setting's own.</summary>
	[ObservableProperty] public partial string Description { get; set; } = "";

	/// <summary>While it runs: the button shows a progress ring instead of its label, and can't be pressed.</summary>
	[ObservableProperty] public partial bool IsBusy { get; set; }
}
