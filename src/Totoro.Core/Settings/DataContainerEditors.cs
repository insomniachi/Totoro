using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Totoro.Core.Settings;

/// <summary>
/// Names of the editors a <see cref="DataContainerProperty"/> can ask for (its EditorType). Set one on a settings
/// property with <c>[Editor(DataContainerEditors.FileBrowser, "")]</c>, or with the property builder's WithEditorType.
/// </summary>
public static class DataContainerEditors
{
	public const string TextBox = "TextBox";
	public const string ToggleSwitch = "ToggleSwitch";
	public const string NumberBox = "NumberBox";
	public const string ComboBox = "ComboBox";
	public const string FileBrowser = "FileBrowser";

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
