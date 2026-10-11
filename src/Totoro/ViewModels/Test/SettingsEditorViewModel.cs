using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Totoro.Core.Dialogs;
using Totoro.Core.Settings;

namespace Totoro.ViewModels.Test;

/// <summary>
/// A playground for the settings editor (<c>DataContainerEditor</c>): edits apply straight away inline, and the dialog
/// version edits a copy that's applied only when saved.
/// </summary>
public partial class SettingsEditorViewModel : ObservableObject
{
	private readonly IDialogService _dialogs;

	public SettingsEditorViewModel(IDialogService dialogs)
	{
		_dialogs = dialogs;
		Settings = new EditorDemoSettings().ToDataContainer();
		foreach (var property in Settings)
		{
			property.PropertyChanged += (_, _) => UpdateValues();
		}

		UpdateValues();
	}

	/// <summary>The demo's properties, shown by an inline editor and editable in a dialog.</summary>
	public DataContainer Settings { get; }

	/// <summary>The properties' current values and types, to show that edits keep each value's type.</summary>
	[ObservableProperty]
	public partial string Values { get; private set; } = "";

	[ObservableProperty]
	public partial string DialogResult { get; private set; } = "";

	[RelayCommand]
	private async Task EditInDialogAsync()
	{
		var saved = await _dialogs.EditAsync("Settings editor demo", Settings);
		DialogResult = saved
			? "Saved: the inline editor and the values show the changes."
			: "Cancelled: nothing changed.";
	}

	private void UpdateValues()
	{
		Values = string.Join(Environment.NewLine,
							 Settings.Select(x => $"{x.Name} = {(x.Value is string s ? $"\"{s}\"" : x.Value)}  ({x.Value?.GetType().Name ?? "null"})"));
	}
}

/// <summary>Settings for the editor demo: one property per kind of editor, configured the way module settings are.</summary>
public sealed class EditorDemoSettings : DataContainerConfig
{
	[DisplayName("Display name")]
	[Description("A string gets a text box.")]
	public string DisplayName { get; set; } = "Totoro";

	[DisplayName("Autoplay")]
	[Description("A bool gets a toggle switch.")]
	[Icon("Play")]
	public bool Autoplay { get; set; } = true;

	[DisplayName("Episodes per page")]
	[Description("An int gets a number box, and stays a whole number.")]
	public int PageSize { get; set; } = 24;

	[DisplayName("Playback speed")]
	[Description("A double gets a number box too.")]
	[Icon("Clock")]
	public double Speed { get; set; } = 1.25;

	[DisplayName("Quality")]
	[Description("An enum gets a dropdown of its values, labelled by their [Description].")]
	public DemoQuality Quality { get; set; } = DemoQuality.FullHd;

	[DisplayName("Server")]
	[Description("[AllowedValues] gets a dropdown of those values.")]
	[AllowedValues("Auto", "Tokyo", "Frankfurt", "Virginia")]
	public string Server { get; set; } = "Auto";

	[DisplayName("Media player")]
	[Description("[Editor(DataContainerEditors.FileBrowser, \"\")] gets a file picker.")]
	[Editor(DataContainerEditors.FileBrowser, "")]
	public string PlayerPath { get; set; } = "";
}

public enum DemoQuality
{
	[Description("Best available")] Best,
	[Description("1080p")] FullHd,
	[Description("720p")] Hd,
	[Description("Data saver (480p)")] DataSaver,
}
