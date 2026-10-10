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
		// The dialog's full content width (548 less padding), so the cards keep their editors beside the headers
		// instead of wrapping them underneath.
		MinWidth = 500;
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
