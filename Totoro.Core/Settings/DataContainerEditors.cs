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
}
