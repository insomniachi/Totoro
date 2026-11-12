using Microsoft.UI.Xaml.Controls;
using Totoro.Core.Dialogs;
using Totoro.UI.Controls;

namespace Totoro.UI.Dialogs;

/// <summary>The dialog for <see cref="DataContainerDialogViewModel"/>: a <see cref="DataContainerEditor"/> over its copies.</summary>
public sealed partial class DataContainerDialog : UserControl
{
	private readonly DataContainerEditor _editor = new();

	public DataContainerDialog()
	{
		Content = _editor;
		IsTabStop = false;
		// Wider than a dialog's usual 500 (the dialog service widens it to fit), so the cards keep their editors beside the
		// headers instead of wrapping them underneath, with room for wide ones (keycaps, a slider and its value, chips).
		MinWidth = 640;
	}

	/// <summary>Set by the dialog service.</summary>
	public DataContainerDialogViewModel? ViewModel
	{
		get;
		set
		{
			field = value;
			_editor.Properties = value?.Properties;
		}
	}
}
